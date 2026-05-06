// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Workflow methods for Phase 3 interoperability subsystems:
///   Lexicon Utility (File #757, LEX*.m)
///   Master Patient Index (File #985, MPIF001.m)
///   Lab EDI (reference lab electronic ordering)
/// </summary>
public partial class PatientWorkflowGrain
{
    // ─── Lexicon Utility ──────────────────────────────────────────────────────

    public async Task<List<LexiconIndexEntry>> SearchLexiconAsync(
        string searchText, string? codingSystem, int maxResults)
    {
        ILexiconSearchGrain searchGrain = GrainFactory.GetGrain<ILexiconSearchGrain>("LEX-INDEX");
        return await searchGrain.SearchAsync(searchText, codingSystem, maxResults);
    }

    public async Task<LexiconIndexEntry?> LookupLexiconCodeAsync(string code, string codingSystem)
    {
        ILexiconSearchGrain searchGrain = GrainFactory.GetGrain<ILexiconSearchGrain>("LEX-INDEX");
        return await searchGrain.LookupCodeAsync(code, codingSystem);
    }

    // ─── Master Patient Index ─────────────────────────────────────────────────

    public async Task<MpiCorrelationState> GetMpiCorrelationAsync()
    {
        // Look up this patient's ICN from PatientState
        IPatientGrain patientGrain = GetPatientGrain();
        PatientState patientState = await patientGrain.GetPatientAsync();

        if (string.IsNullOrEmpty(patientState.Icn))
            return new MpiCorrelationState();

        IMpiCorrelationGrain mpiGrain = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{patientState.Icn}");
        return await mpiGrain.GetCorrelationAsync();
    }

    public async Task SetMpiCorrelationAsync(string icn, string ssn, DateTime? dateOfBirth, string? sex)
    {
        // Set ICN on the patient grain
        IPatientGrain patientGrain = GetPatientGrain();
        await patientGrain.SetIcnAsync(icn);

        // Get the patient name for the correlation record
        PatientState patientState = await patientGrain.GetPatientAsync();

        // Create/update the MPI correlation
        IMpiCorrelationGrain mpiGrain = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");
        await mpiGrain.SetCorrelationAsync(icn, patientState.Name, ssn, dateOfBirth, sex);

        // Update the MPI search index
        IMpiSearchGrain searchGrain = GrainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");
        await searchGrain.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = patientState.Name,
            Ssn = ssn,
            DateOfBirth = dateOfBirth,
            Sex = sex,
            FacilityCount = 0,
            IsDeceased = false
        });
    }

    public async Task<List<MpiLocalCorrelation>> GetMpiTreatingFacilitiesAsync()
    {
        IPatientGrain patientGrain = GetPatientGrain();
        PatientState patientState = await patientGrain.GetPatientAsync();

        if (string.IsNullOrEmpty(patientState.Icn))
            return new List<MpiLocalCorrelation>();

        IMpiCorrelationGrain mpiGrain = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{patientState.Icn}");
        return await mpiGrain.GetTreatingFacilitiesAsync();
    }

    // ─── Lab EDI ──────────────────────────────────────────────────────────────

    public async Task<List<LabEdiOrderSummary>> GetLabEdiOrdersAsync(int maxResults)
    {
        ILabEdiIndexGrain indexGrain = GrainFactory.GetGrain<ILabEdiIndexGrain>("LAB-EDI-INDEX");
        return await indexGrain.GetOrdersByPatientAsync(PatientId, maxResults);
    }
}
