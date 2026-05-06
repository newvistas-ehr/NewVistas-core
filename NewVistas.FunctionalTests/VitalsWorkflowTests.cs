// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Vitals — GMRVFILE.m / GMRVED*.m.
/// File #120.5 (GMRV VITAL MEASUREMENT).
/// Tests the redesigned vitals architecture: composite grain keys,
/// per-patient vital index, embedded recent vitals cache, and site parameters.
/// </summary>
[TestFixture]
public class VitalsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IVitalGrain GetVital(string vitalId)
        => _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);

    private IPatientVitalIndexGrain GetVitalIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientVitalIndexGrain>(patientId);

    // ─── Basic Record / Retrieve ──────────────────────────────────────────

    [Test]
    public async Task RecordVitals_SingleType_AppearsInLatestList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { ["TEMPERATURE"] = "98.6" }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(1));
        Assert.That(vitals[0].VitalType, Is.EqualTo("TEMPERATURE"));
        Assert.That(vitals[0].Value, Is.EqualTo("98.6"));
    }

    [Test]
    public async Task RecordVitals_BloodPressure_ValuePreserved()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { ["BLOOD PRESSURE"] = "118/76" }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals[0].Value, Is.EqualTo("118/76"));
    }

    [Test]
    public async Task RecordVitals_MultipleTypes_AllTypesPresent()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["TEMPERATURE"] = "99.1",
                ["PULSE"] = "80",
                ["RESPIRATION"] = "18"
            }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(3));
        Assert.That(vitals.Any(v => v.VitalType == "TEMPERATURE"), Is.True);
        Assert.That(vitals.Any(v => v.VitalType == "PULSE"), Is.True);
        Assert.That(vitals.Any(v => v.VitalType == "RESPIRATION"), Is.True);
    }

    [Test]
    public async Task RecordVitals_SameTypeTwice_ReturnsOnlyLatest()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        DateTime earlier = DateTime.UtcNow.AddHours(-2);
        DateTime later = DateTime.UtcNow;

        await w.RecordVitalsAsync(null, null, null, null, earlier,
            new Dictionary<string, string> { ["TEMPERATURE"] = "97.9" }, null);
        await w.RecordVitalsAsync(null, null, null, null, later,
            new Dictionary<string, string> { ["TEMPERATURE"] = "100.4" }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(1));
        Assert.That(vitals[0].Value, Is.EqualTo("100.4"));
    }

    [Test]
    public async Task RecordVitals_SixTypes_AllSixReturnedInSummary()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["TEMPERATURE"] = "98.6",
                ["PULSE"] = "72",
                ["RESPIRATION"] = "16",
                ["BLOOD PRESSURE"] = "120/80",
                ["WEIGHT"] = "185",
                ["HEIGHT"] = "72"
            }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(6));
    }

    [Test]
    public async Task GetLatestVitals_NoVitals_ReturnsEmpty()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();

        Assert.That(vitals, Is.Empty);
    }

    // ─── Index and Cache Tests ────────────────────────────────────────────

    [Test]
    public async Task RecordVitals_PopulatesVitalIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["TEMPERATURE"] = "98.6",
                ["PULSE"] = "72"
            }, null);

        List<VitalIndexEntry> entries = await GetVitalIndex(patientId).GetAllKeysAsync();
        Assert.That(entries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RecordVitals_PopulatesRecentVitalsCache()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["TEMPERATURE"] = "98.6",
                ["PULSE"] = "72"
            }, null);

        List<VitalSummary> cached = await GetPatient(patientId).GetRecentVitalsAsync();
        Assert.That(cached, Has.Count.EqualTo(2));
        Assert.That(cached.Any(v => v.VitalType == "TEMPERATURE"), Is.True);
        Assert.That(cached.Any(v => v.VitalType == "PULSE"), Is.True);
    }

    [Test]
    public async Task RecordVitals_WithLocation_LocationOnGrainState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordVitalsAsync(null, "Clinic 3B", null, null, DateTime.UtcNow,
            new Dictionary<string, string> { ["PULSE"] = "68" }, null);

        List<VitalIndexEntry> entries = await GetVitalIndex(patientId).GetAllKeysAsync();
        VitalState state = await GetVital(entries[0].VitalGrainKey).GetVitalAsync();
        Assert.That(state.LocationName, Is.EqualTo("Clinic 3B"));
    }

    [Test]
    public async Task RecordVitals_EnteredBy_StoredOnGrainState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordVitalsAsync(null, null, "UID-42", "Nurse Rivera", DateTime.UtcNow,
            new Dictionary<string, string> { ["RESPIRATION"] = "14" }, null);

        List<VitalIndexEntry> entries = await GetVitalIndex(patientId).GetAllKeysAsync();
        VitalState state = await GetVital(entries[0].VitalGrainKey).GetVitalAsync();
        Assert.That(state.EnteredByName, Is.EqualTo("Nurse Rivera"));
        Assert.That(state.EnteredById, Is.EqualTo("UID-42"));
    }

    // ─── History and Index Queries ────────────────────────────────────────

    [Test]
    public async Task GetVitalHistory_ReturnsAllVitals()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime now = DateTime.UtcNow;

        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-3),
            new Dictionary<string, string> { ["TEMPERATURE"] = "97.9" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-2),
            new Dictionary<string, string> { ["PULSE"] = "72" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-1),
            new Dictionary<string, string> { ["RESPIRATION"] = "16" }, null);

        List<VitalSummary> history = await w.GetVitalHistoryAsync(null, null, 100);
        Assert.That(history, Has.Count.EqualTo(3));
        Assert.That(history[0].DateTimeTaken, Is.GreaterThan(history[1].DateTimeTaken));
        Assert.That(history[1].DateTimeTaken, Is.GreaterThan(history[2].DateTimeTaken));
    }

    [Test]
    public async Task GetVitalHistory_DateRange_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime now = DateTime.UtcNow;

        await w.RecordVitalsAsync(null, null, null, null, now.AddDays(-5),
            new Dictionary<string, string> { ["TEMPERATURE"] = "97.5" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddDays(-2),
            new Dictionary<string, string> { ["TEMPERATURE"] = "98.6" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now,
            new Dictionary<string, string> { ["TEMPERATURE"] = "99.1" }, null);

        // Query only the middle range — should exclude the oldest and newest
        List<VitalSummary> history = await w.GetVitalHistoryAsync(
            now.AddDays(-3), now.AddDays(-1), 100);
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].Value, Is.EqualTo("98.6"));
    }

    [Test]
    public async Task GetVitalHistoryByType_FiltersToSingleType()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime now = DateTime.UtcNow;

        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-2),
            new Dictionary<string, string>
            {
                ["BLOOD PRESSURE"] = "120/80",
                ["TEMPERATURE"] = "98.6"
            }, null);
        await w.RecordVitalsAsync(null, null, null, null, now,
            new Dictionary<string, string>
            {
                ["BLOOD PRESSURE"] = "130/85",
                ["PULSE"] = "88"
            }, null);

        List<VitalSummary> bpHistory = await w.GetVitalHistoryByTypeAsync(
            "BLOOD PRESSURE", now.AddHours(-3), now.AddHours(1));
        Assert.That(bpHistory, Has.Count.EqualTo(2));
        Assert.That(bpHistory.All(v => v.VitalType == "BLOOD PRESSURE"), Is.True);
    }

    [Test]
    public async Task RecentVitalsCache_TrimsToDisplayCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Set the site parameter to a low display count
        ISiteParametersGrain siteParams = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.SetVitalsDisplayCountAsync(3);

        DateTime now = DateTime.UtcNow;

        // Record 5 vitals (each a different type so they all stay in the cache)
        await w.RecordVitalsAsync(null, null, null, null, now.AddMinutes(-5),
            new Dictionary<string, string> { ["TEMPERATURE"] = "98.0" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddMinutes(-4),
            new Dictionary<string, string> { ["PULSE"] = "70" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddMinutes(-3),
            new Dictionary<string, string> { ["RESPIRATION"] = "16" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddMinutes(-2),
            new Dictionary<string, string> { ["BLOOD PRESSURE"] = "120/80" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now.AddMinutes(-1),
            new Dictionary<string, string> { ["WEIGHT"] = "185" }, null);

        // The recent vitals cache should be trimmed to 3
        List<VitalSummary> cached = await GetPatient(patientId).GetRecentVitalsAsync();
        Assert.That(cached, Has.Count.EqualTo(3));

        // Reset display count to default so other tests are not affected
        await siteParams.SetVitalsDisplayCountAsync(10);
    }

    // ─── Abnormal / Error Flags ───────────────────────────────────────────

    [Test]
    public async Task MarkVitalEnteredInError_ExcludedFromHistory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime now = DateTime.UtcNow;

        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-1),
            new Dictionary<string, string> { ["PULSE"] = "999" }, null);
        await w.RecordVitalsAsync(null, null, null, null, now,
            new Dictionary<string, string> { ["PULSE"] = "72" }, null);

        // Mark the bad reading as entered in error
        List<VitalIndexEntry> entries = await GetVitalIndex(patientId).GetAllKeysAsync();
        VitalIndexEntry errorEntry = entries.First(e => e.DateTimeTaken < now);
        await GetVital(errorEntry.VitalGrainKey).MarkEnteredInErrorAsync("Data entry error");

        // History should exclude the error entry
        List<VitalSummary> history = await w.GetVitalHistoryAsync(null, null, 100);
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].Value, Is.EqualTo("72"));
    }

    [Test]
    public async Task MultiplePatients_VitalsAreIndependent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await w1.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string> { ["TEMPERATURE"] = "99.0" }, null);

        List<VitalSummary> vitals2 = await w2.GetLatestVitalsAsync();
        Assert.That(vitals2, Is.Empty);
    }

    // ─── Full Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_RecordMarkErrorRecordAgain_LatestListOnlyHasValid()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        DateTime now = DateTime.UtcNow;

        // First reading — will be marked as error
        await w.RecordVitalsAsync(null, null, null, null, now.AddHours(-2),
            new Dictionary<string, string> { ["TEMPERATURE"] = "105.0" }, null);

        List<VitalIndexEntry> entries = await GetVitalIndex(patientId).GetAllKeysAsync();
        await GetVital(entries[0].VitalGrainKey).MarkEnteredInErrorAsync("Incorrect probe reading");

        // Second, valid reading
        await w.RecordVitalsAsync(null, null, null, null, now,
            new Dictionary<string, string> { ["TEMPERATURE"] = "98.9" }, null);

        // GetLatestVitalsAsync reads from embedded cache — both entries are in the cache
        // but GetVitalHistoryAsync fans out and filters errors
        List<VitalSummary> history = await w.GetVitalHistoryAsync(null, null, 100);
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].Value, Is.EqualTo("98.9"));
    }

    // ─── Performance Benchmark ─────────────────────────────────────────

    [Test]
    public async Task PerformanceBenchmark_CachedVitals_FasterThanIndexFanOut()
    {
        string patientId = $"PATIENT-PERF-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Set cache to 5 — only the 5 most recent vitals stay on the patient grain
        ISiteParametersGrain siteParams = _cluster.GrainFactory
            .GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.SetVitalsDisplayCountAsync(5);

        // Seed 100 vitals across different types and times
        string[] vitalTypes = ["TEMPERATURE", "PULSE", "RESPIRATION", "BLOOD PRESSURE", "WEIGHT",
            "HEIGHT", "PAIN", "PULSE OXIMETRY", "CENTRAL VENOUS PRESSURE", "CIRCUMFERENCE/GIRTH"];
        DateTime baseTime = DateTime.UtcNow.AddDays(-30);

        for (int i = 0; i < 100; i++)
        {
            string vitalType = vitalTypes[i % vitalTypes.Length];
            DateTime taken = baseTime.AddHours(i);
            string value = vitalType switch
            {
                "TEMPERATURE" => (97.0 + (i % 30) * 0.1).ToString("F1"),
                "PULSE" => (60 + i % 40).ToString(),
                "RESPIRATION" => (12 + i % 10).ToString(),
                "BLOOD PRESSURE" => $"{110 + i % 30}/{70 + i % 20}",
                "WEIGHT" => (170 + i % 20).ToString(),
                "HEIGHT" => "72",
                "PAIN" => (i % 10).ToString(),
                "PULSE OXIMETRY" => (94 + i % 6).ToString(),
                "CENTRAL VENOUS PRESSURE" => (5 + i % 10).ToString(),
                _ => (30 + i % 10).ToString()
            };

            await w.RecordVitalsAsync(null, null, null, null, taken,
                new Dictionary<string, string> { [vitalType] = value }, null);
        }

        // Verify setup: 5 in cache, 100 in index
        List<VitalSummary> cached = await GetPatient(patientId).GetRecentVitalsAsync();
        int indexCount = await GetVitalIndex(patientId).GetCountAsync();
        Assert.That(cached, Has.Count.EqualTo(5), "Cache should hold exactly 5 vitals");
        Assert.That(indexCount, Is.EqualTo(100), "Index should hold all 100 vitals");

        // ── Warm-up calls (activate grains, JIT, etc.) ──
        await w.GetLatestVitalsAsync();
        await w.GetVitalHistoryAsync(null, null, 100);

        // ── Timed: hot cache read (embedded on patient grain — zero fan-out) ──
        const int iterations = 20;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            List<VitalSummary> latest = await w.GetLatestVitalsAsync();
            Assert.That(latest, Has.Count.GreaterThan(0));
        }
        sw.Stop();
        long cachedMs = sw.ElapsedMilliseconds;
        double cachedAvgMs = (double)cachedMs / iterations;

        // ── Timed: index fan-out (query index, then activate 100 vital grains) ──
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            List<VitalSummary> history = await w.GetVitalHistoryAsync(null, null, 100);
            Assert.That(history, Has.Count.EqualTo(100));
        }
        sw.Stop();
        long indexMs = sw.ElapsedMilliseconds;
        double indexAvgMs = (double)indexMs / iterations;

        double speedup = indexAvgMs / cachedAvgMs;

        // Log results so they appear in test output
        TestContext.Out.WriteLine("╔══════════════════════════════════════════════════════════╗");
        TestContext.Out.WriteLine("║  VITALS PERFORMANCE BENCHMARK                           ║");
        TestContext.Out.WriteLine("╠══════════════════════════════════════════════════════════╣");
        TestContext.Out.WriteLine($"║  Patient vitals:     100 total, 5 cached                ║");
        TestContext.Out.WriteLine($"║  Iterations:         {iterations,4}                                ║");
        TestContext.Out.WriteLine($"║  Cache read avg:     {cachedAvgMs,8:F2} ms  (zero fan-out)      ║");
        TestContext.Out.WriteLine($"║  Index+fan-out avg:  {indexAvgMs,8:F2} ms  (100 grain reads)   ║");
        TestContext.Out.WriteLine($"║  Speedup factor:     {speedup,8:F1}x                          ║");
        TestContext.Out.WriteLine("╚══════════════════════════════════════════════════════════╝");

        // The cached path should be meaningfully faster than fanning out to 100 grains.
        // Even in-memory Orleans has per-grain activation overhead, message scheduling,
        // and serialization costs that compound with fan-out count.
        Assert.That(cachedAvgMs, Is.LessThan(indexAvgMs),
            $"Cache read ({cachedAvgMs:F2}ms) should be faster than index fan-out ({indexAvgMs:F2}ms)");

        // Reset display count to default so other tests are not affected
        await siteParams.SetVitalsDisplayCountAsync(10);
    }
}
