// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers.HL7v2;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// TCP listener service for automated lab instruments. Runs as an IHostedService,
/// opening one TCP listener per active instrument on its configured port.
///
/// Implements MLLP (Minimal Lower Layer Protocol) framing — the standard transport
/// for HL7v2 messages over TCP/IP:
///   - Start block: \x0B (VT)
///   - End block: \x1C (FS) + \x0D (CR)
///
/// Mirrors VistA's LAHWATCH.m / LAWATCH.m background watcher that monitors
/// instrument connections, and the LA7VIN.m EN entry point that processes
/// incoming messages.
///
/// Inbound flow: Instrument → TCP → MLLP unwrap → IInstrumentMessageQueueGrain
/// → HL7Parser → ILabResultIngestion (existing grain)
/// </summary>
public class InstrumentListenerService : IHostedService, IDisposable
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InstrumentListenerService> _logger;
    private readonly Dictionary<string, TcpListener> _listeners = new();
    private readonly List<Task> _acceptTasks = new();
    private CancellationTokenSource? _cts;

    public InstrumentListenerService(
        IGrainFactory grainFactory,
        ILogger<InstrumentListenerService> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            IInstrumentIndexGrain index = _grainFactory.GetGrain<IInstrumentIndexGrain>("LA-INST-INDEX");
            List<InstrumentEntry> activeInstruments = await index.GetActiveInstrumentsAsync();

            foreach (InstrumentEntry instrument in activeInstruments)
            {
                if (instrument.TcpPort <= 0) continue;

                try
                {
                    TcpListener listener = new(IPAddress.Any, instrument.TcpPort);
                    listener.Start();
                    _listeners[instrument.InstrumentId] = listener;

                    _logger.LogInformation(
                        "Instrument listener started: {Name} ({Id}) on port {Port}",
                        instrument.Name, instrument.InstrumentId, instrument.TcpPort);

                    // Start accepting connections in background
                    _acceptTasks.Add(AcceptConnectionsAsync(
                        instrument.InstrumentId, listener, _cts.Token));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to start listener for instrument {Name} on port {Port}",
                        instrument.Name, instrument.TcpPort);
                }
            }

            _logger.LogInformation(
                "Instrument Listener Service started with {Count} active listeners",
                _listeners.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Instrument Listener Service");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Instrument Listener Service stopping...");

        _cts?.Cancel();

        foreach (KeyValuePair<string, TcpListener> kvp in _listeners)
        {
            try
            {
                kvp.Value.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping listener for {Id}", kvp.Key);
            }
        }

        if (_acceptTasks.Count > 0)
        {
            await Task.WhenAll(_acceptTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _listeners.Clear();
        _acceptTasks.Clear();

        _logger.LogInformation("Instrument Listener Service stopped.");
    }

    /// <summary>
    /// Accepts TCP connections for one instrument and processes MLLP-framed HL7 messages.
    /// </summary>
    private async Task AcceptConnectionsAsync(
        string instrumentId, TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync(ct);
                // Handle each connection without blocking the accept loop
                _ = HandleConnectionAsync(instrumentId, client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error accepting connection for instrument {Id}", instrumentId);
            }
        }
    }

    /// <summary>
    /// Handles a single TCP connection from an instrument.
    /// Reads MLLP-framed messages, enqueues them, processes them, and sends ACKs.
    /// </summary>
    private async Task HandleConnectionAsync(
        string instrumentId, TcpClient client, CancellationToken ct)
    {
        string remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.LogInformation(
            "Instrument connection from {Endpoint} for {Id}",
            remoteEndpoint, instrumentId);

        // Record connection on the instrument grain
        string instKey = instrumentId;
        IAutoInstrumentGrain inst = _grainFactory.GetGrain<IAutoInstrumentGrain>(instKey);
        await inst.RecordConnectionAsync();

        // Get the message queue grain
        string mqKey = instrumentId.Replace("LA-INST:", "LA-MQ:");
        IInstrumentMessageQueueGrain queue = _grainFactory.GetGrain<IInstrumentMessageQueueGrain>(mqKey);

        try
        {
            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[65536];
            StringBuilder messageBuilder = new();
            bool inMessage = false;

            while (!ct.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, ct);
                if (bytesRead == 0) break; // Connection closed

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                foreach (char c in chunk)
                {
                    if (c == '\x0B') // Start of MLLP message
                    {
                        inMessage = true;
                        messageBuilder.Clear();
                    }
                    else if (c == '\x1C' && inMessage) // End of MLLP message
                    {
                        inMessage = false;
                        string rawHL7 = messageBuilder.ToString();

                        if (!string.IsNullOrWhiteSpace(rawHL7))
                        {
                            try
                            {
                                // Enqueue and process
                                await queue.EnqueueMessageAsync(rawHL7);
                                await queue.ProcessNextAsync();

                                // Send ACK
                                string controlId = ExtractControlId(rawHL7);
                                string ack = HL7Builder.BuildACK(controlId);
                                string mllpAck = HL7Builder.WrapMllp(ack);
                                byte[] ackBytes = Encoding.UTF8.GetBytes(mllpAck);
                                await stream.WriteAsync(ackBytes, ct);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Error processing message from {Id}", instrumentId);

                                // Send NACK
                                string controlId = ExtractControlId(rawHL7);
                                string nack = HL7Builder.BuildACK(controlId, "AE",
                                    errorMessage: ex.Message);
                                string mllpNack = HL7Builder.WrapMllp(nack);
                                byte[] nackBytes = Encoding.UTF8.GetBytes(mllpNack);
                                await stream.WriteAsync(nackBytes, ct);
                            }
                        }

                        messageBuilder.Clear();
                    }
                    else if (c != '\x0D' || inMessage) // Skip trailing CR after \x1C
                    {
                        if (inMessage)
                            messageBuilder.Append(c);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling connection from {Endpoint} for {Id}",
                remoteEndpoint, instrumentId);
        }
        finally
        {
            client.Close();
            _logger.LogInformation(
                "Instrument connection closed: {Endpoint} for {Id}",
                remoteEndpoint, instrumentId);
        }
    }

    /// <summary>
    /// Quick extraction of MSH-10 (Message Control ID) from raw HL7 for ACK construction.
    /// </summary>
    private static string ExtractControlId(string rawHL7)
    {
        try
        {
            HL7Message msg = HL7Parser.Parse(rawHL7);
            return msg.MessageControlId ?? "UNKNOWN";
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        foreach (TcpListener listener in _listeners.Values)
        {
            try { listener.Stop(); } catch { /* cleanup */ }
        }
        _listeners.Clear();
    }
}
