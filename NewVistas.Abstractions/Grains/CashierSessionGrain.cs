// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CashierSessionGrain : Grain, ICashierSessionGrain
{
    private readonly IPersistentState<CashierSessionState> _state;

    public CashierSessionGrain(
        [PersistentState("cashierSessionState", "cashierSessionStore")]
        IPersistentState<CashierSessionState> state)
    {
        _state = state;
    }

    public Task<CashierSessionState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task OpenAsync(
        string stationId,
        string stationName,
        string cashierId,
        string cashierName,
        DateTime sessionDate,
        decimal openingBalance)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.SessionId       = this.GetPrimaryKeyString();
        _state.State.StationId       = stationId;
        _state.State.StationName     = stationName;
        _state.State.CashierId       = cashierId;
        _state.State.CashierName     = cashierName;
        _state.State.SessionDate     = sessionDate.Date;
        _state.State.Status          = CashierSessionStatus.Open;
        _state.State.OpeningBalance  = openingBalance;
        _state.State.ExpectedBalance = openingBalance;
        _state.State.OpenedDate      = now;
        _state.State.CreatedDate     = now;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public async Task RecordReceiptAsync(
        string receiptId,
        decimal amount,
        string paymentMethod,
        DateTime receiptDate)
    {
        _state.State.ReceiptEntries.Add(new CashierSessionReceiptEntry
        {
            ReceiptId     = receiptId,
            Amount        = amount,
            PaymentMethod = paymentMethod,
            ReceiptDate   = receiptDate,
        });

        _state.State.TotalCollected += amount;

        switch (paymentMethod)
        {
            case "Cash":
                _state.State.TotalCashCollected += amount;
                break;
            case "Check":
                _state.State.TotalCheckCollected += amount;
                break;
            case "MoneyOrder":
                _state.State.TotalMoneyOrderCollected += amount;
                break;
            default:
                _state.State.TotalOtherCollected += amount;
                break;
        }

        _state.State.ExpectedBalance  = _state.State.OpeningBalance + _state.State.TotalCollected;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseAsync(decimal actualBalance, string? notes)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.Status           = CashierSessionStatus.Closed;
        _state.State.ActualBalance    = actualBalance;
        _state.State.Discrepancy      = _state.State.ExpectedBalance - actualBalance;
        _state.State.ClosedDate       = now;
        _state.State.Notes            = notes;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public async Task TurnInAsync(decimal turnedInAmount, string turnedInToUserId, string? turnedInReceiptNumber)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.Status                  = CashierSessionStatus.TurnedIn;
        _state.State.TurnedInAmount          = turnedInAmount;
        _state.State.TurnedInToUserId        = turnedInToUserId;
        _state.State.TurnedInReceiptNumber   = turnedInReceiptNumber;
        _state.State.TurnedInDate            = now;
        _state.State.LastModifiedDate        = now;
        await _state.WriteStateAsync();
    }
}
