// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// FHIR R4 Gateway — read-only FHIR-compliant endpoints that expose
/// existing NewVistas grain data as HL7 FHIR R4 JSON resources.
///
/// This mirrors the VistA Integration Adapter (VIA) and Virtual Patient
/// Record (VPR) functionality that exposes VistA data to external systems
/// via standard FHIR interfaces (VPRDJ.m, VIABRPC.m).
///
/// Supported FHIR Resources:
///   Patient, Condition, AllergyIntolerance, Observation (vitals),
///   MedicationRequest, DiagnosticReport (labs), Encounter, Appointment
///
/// Conforms to US Core Implementation Guide profiles where applicable.
/// All responses use application/fhir+json media type.
/// </summary>
[Authorize]
[ApiController]
[Route("api/fhir")]
[Produces("application/json")]
public class FhirController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<FhirController> _logger;

    public FhirController(IGrainFactory grainFactory, ILogger<FhirController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Capability Statement ─────────────────────────────────────────────────

    /// <summary>
    /// FHIR CapabilityStatement (metadata endpoint).
    /// Describes this server's supported resources and operations.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("metadata")]
    public IActionResult GetCapabilityStatement()
    {
        string baseUrl = $"{Request.Scheme}://{Request.Host}";

        var capability = new
        {
            resourceType = "CapabilityStatement",
            status = "active",
            date = "2026-03-15",
            kind = "instance",
            fhirVersion = "4.0.1",
            format = new[] { "json" },
            implementation = new
            {
                description = "NewVistas FHIR R4 Gateway — VistA-inspired Clinical Information System",
                url = $"{baseUrl}/api/fhir"
            },
            rest = new[]
            {
                new
                {
                    mode = "server",
                    security = new
                    {
                        extension = new[]
                        {
                            new
                            {
                                url = "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris",
                                extension = new object[]
                                {
                                    new { url = "authorize", valueUri = $"{baseUrl}/api/smart/authorize" },
                                    new { url = "token", valueUri = $"{baseUrl}/api/smart/token" },
                                    new { url = "introspect", valueUri = $"{baseUrl}/api/smart/introspect" },
                                    new { url = "revoke", valueUri = $"{baseUrl}/api/smart/revoke" },
                                    new { url = "register", valueUri = $"{baseUrl}/api/smart/register" }
                                }
                            }
                        },
                        service = new[]
                        {
                            new
                            {
                                coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/restful-security-service", code = "SMART-on-FHIR", display = "SMART on FHIR" } },
                                text = "OAuth2 using SMART-on-FHIR profile (see http://docs.smarthealthit.org)"
                            }
                        },
                        description = "SMART on FHIR 2.0.0 — OAuth 2.0 authorization with PKCE, token introspection, and patient revocation per §170.315(g)(10)."
                    },
                    resource = new object[]
                    {
                        new { type = "Patient", interaction = new[] { new { code = "read" }, new { code = "search-type" } } },
                        new { type = "Condition", interaction = new[] { new { code = "search-type" } } },
                        new { type = "AllergyIntolerance", interaction = new[] { new { code = "search-type" } } },
                        new { type = "Observation", interaction = new[] { new { code = "search-type" } } },
                        new { type = "MedicationRequest", interaction = new[] { new { code = "search-type" } } },
                        new { type = "DiagnosticReport", interaction = new[] { new { code = "search-type" } } },
                        new { type = "Encounter", interaction = new[] { new { code = "search-type" } } },
                        new { type = "Appointment", interaction = new[] { new { code = "search-type" } } },
                    },
                    operation = new[]
                    {
                        new { name = "export", definition = "http://hl7.org/fhir/uv/bulkdata/OperationDefinition/group-export" }
                    }
                }
            }
        };

        return Ok(capability);
    }

    // ─── Patient Resource ─────────────────────────────────────────────────────

    /// <summary>
    /// FHIR Patient read — GET /api/fhir/Patient/{id}
    /// Maps VistA PATIENT file (#2) to FHIR Patient (US Core profile).
    /// </summary>
    [HttpGet("Patient/{patientId}")]
    public async Task<IActionResult> GetPatient(string patientId)
    {
        try
        {
            PatientState state = await W(patientId).GetPatientAsync();
            if (string.IsNullOrEmpty(state.Name))
                return NotFound(FhirOperationOutcome("not-found", $"Patient {patientId} not found"));

            return Ok(MapPatientToFhir(patientId, state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Patient read error for {PatientId}", patientId);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    /// <summary>
    /// FHIR Patient search — GET /api/fhir/Patient?name={name}
    /// Mirrors ORWPT LOOKUP via MPI search.
    /// </summary>
    [HttpGet("Patient")]
    public async Task<IActionResult> SearchPatients([FromQuery] string? name, [FromQuery] int _count = 25)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return Ok(FhirBundle("searchset", new List<object>()));

            // StatelessWorker search reader — scales out instead of funneling
            // through the singleton PATIENT-INDEX (or a "SEARCH" workflow grain).
            List<PatientIndexEntry> results = await _grainFactory
                .GetGrain<IPatientSearchGrain>("PATIENT-SEARCH")
                .SearchAsync(name, _count);
            var entries = new List<object>();

            foreach (PatientIndexEntry entry in results)
            {
                PatientState state = await W(entry.PatientId).GetPatientAsync();
                entries.Add(new
                {
                    fullUrl = $"Patient/{entry.PatientId}",
                    resource = MapPatientToFhir(entry.PatientId, state)
                });
            }

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Patient search error");
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── Condition Resource (Problem List) ────────────────────────────────────

    /// <summary>
    /// FHIR Condition search — GET /api/fhir/Condition?patient={patientId}
    /// Maps VistA PROBLEM LIST file (#9000011) to FHIR Condition (US Core).
    /// </summary>
    [HttpGet("Condition")]
    public async Task<IActionResult> SearchConditions([FromQuery] string patient, [FromQuery] string? status)
    {
        try
        {
            List<ProblemSummary> problems = status == "active"
                ? await W(patient).GetActiveProblemsAsync()
                : await W(patient).GetAllProblemsAsync();

            var entries = problems.Select(p => new
            {
                fullUrl = $"Condition/{p.ProblemId}",
                resource = new
                {
                    resourceType = "Condition",
                    id = p.ProblemId,
                    meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-condition-problems-health-concerns" } },
                    clinicalStatus = new
                    {
                        coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/condition-clinical", code = p.Status == "ACTIVE" ? "active" : "inactive" } }
                    },
                    category = new[]
                    {
                        new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/condition-category", code = "problem-list-item", display = "Problem List Item" } } }
                    },
                    code = new
                    {
                        coding = !string.IsNullOrEmpty(p.DiagnosisCode) ? new[] { new { system = "http://hl7.org/fhir/sid/icd-10-cm", code = p.DiagnosisCode } } : Array.Empty<object>(),
                        text = p.Diagnosis
                    },
                    subject = new { reference = $"Patient/{patient}" },
                    onsetDateTime = p.DateOfOnset?.ToString("yyyy-MM-dd")
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Condition search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── AllergyIntolerance Resource ──────────────────────────────────────────

    /// <summary>
    /// FHIR AllergyIntolerance search — GET /api/fhir/AllergyIntolerance?patient={patientId}
    /// Maps VistA PATIENT ALLERGIES file (#120.8) to FHIR AllergyIntolerance.
    /// </summary>
    [HttpGet("AllergyIntolerance")]
    public async Task<IActionResult> SearchAllergies([FromQuery] string patient)
    {
        try
        {
            List<AllergySummary> allergies = await W(patient).GetAllergiesAsync();

            var entries = allergies.Select(a => new
            {
                fullUrl = $"AllergyIntolerance/{a.AllergyId}",
                resource = new
                {
                    resourceType = "AllergyIntolerance",
                    id = a.AllergyId,
                    meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-allergyintolerance" } },
                    clinicalStatus = new
                    {
                        coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", code = "active" } }
                    },
                    type = "allergy",
                    category = new[] { "medication" },
                    criticality = MapAllergyCriticality(a.Severity),
                    code = new { text = a.Allergen },
                    patient = new { reference = $"Patient/{patient}" },
                    reaction = a.Reactions?.Count > 0 ? new[]
                    {
                        new
                        {
                            manifestation = a.Reactions.Select(r => new { text = r }).ToArray(),
                            severity = MapAllergyReactionSeverity(a.Severity)
                        }
                    } : null
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR AllergyIntolerance search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── Observation Resource (Vitals) ────────────────────────────────────────

    /// <summary>
    /// FHIR Observation search — GET /api/fhir/Observation?patient={patientId}&amp;category=vital-signs
    /// Maps VistA GMRV VITAL MEASUREMENT file (#120.5) to FHIR Observation.
    /// </summary>
    [HttpGet("Observation")]
    public async Task<IActionResult> SearchObservations([FromQuery] string patient, [FromQuery] string? category)
    {
        try
        {
            List<VitalSummary> vitals = await W(patient).GetLatestVitalsAsync();

            var entries = vitals.Select(v => new
            {
                fullUrl = $"Observation/{v.VitalType}-{patient}",
                resource = new
                {
                    resourceType = "Observation",
                    id = $"{v.VitalType}-{patient}",
                    meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-vital-signs" } },
                    status = "final",
                    category = new[]
                    {
                        new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "vital-signs", display = "Vital Signs" } } }
                    },
                    code = new
                    {
                        coding = new[] { MapVitalToLoinc(v.VitalType) },
                        text = v.VitalType
                    },
                    subject = new { reference = $"Patient/{patient}" },
                    effectiveDateTime = v.DateTimeTaken.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    valueString = v.Value
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Observation search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── MedicationRequest Resource ───────────────────────────────────────────

    /// <summary>
    /// FHIR MedicationRequest search — GET /api/fhir/MedicationRequest?patient={patientId}
    /// Maps VistA ORDER file (#100) pharmacy orders to FHIR MedicationRequest.
    /// </summary>
    [HttpGet("MedicationRequest")]
    public async Task<IActionResult> SearchMedicationRequests([FromQuery] string patient, [FromQuery] string? status)
    {
        try
        {
            List<MedicationSummary> meds = await W(patient).GetActiveMedicationsAsync();

            var entries = meds.Select(m => new
            {
                fullUrl = $"MedicationRequest/{m.PrescriptionId}",
                resource = new
                {
                    resourceType = "MedicationRequest",
                    id = m.PrescriptionId,
                    meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-medicationrequest" } },
                    status = MapMedStatus(m.Status),
                    intent = "order",
                    medicationCodeableConcept = new { text = m.DrugName },
                    subject = new { reference = $"Patient/{patient}" },
                    authoredOn = m.FillDate?.ToString("yyyy-MM-dd"),
                    dosageInstruction = !string.IsNullOrEmpty(m.Sig) ? new[]
                    {
                        new { text = m.Sig }
                    } : null
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR MedicationRequest search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── DiagnosticReport Resource (Labs) ─────────────────────────────────────

    /// <summary>
    /// FHIR DiagnosticReport search — GET /api/fhir/DiagnosticReport?patient={patientId}
    /// Maps VistA LAB DATA file (#63) to FHIR DiagnosticReport.
    /// </summary>
    [HttpGet("DiagnosticReport")]
    public async Task<IActionResult> SearchDiagnosticReports([FromQuery] string patient)
    {
        try
        {
            List<LabTestSummaryEntry> labResults = await W(patient).GetLabSummaryAsync();

            var entries = labResults.Select(t => new
            {
                fullUrl = $"DiagnosticReport/{t.LoincCode}",
                resource = new
                {
                    resourceType = "DiagnosticReport",
                    id = t.LoincCode,
                    meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-diagnosticreport-lab" } },
                    status = "final",
                    category = new[]
                    {
                        new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/v2-0074", code = "LAB", display = "Laboratory" } } }
                    },
                    code = new
                    {
                        coding = !string.IsNullOrEmpty(t.LoincCode) ? new[] { new { system = "http://loinc.org", code = t.LoincCode } } : Array.Empty<object>(),
                        text = t.TestName
                    },
                    subject = new { reference = $"Patient/{patient}" },
                    effectiveDateTime = t.ResultDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    result = !string.IsNullOrEmpty(t.Value) ? new[]
                    {
                        new
                        {
                            display = $"{t.TestName}: {t.Value} {t.Units}"
                        }
                    } : null
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR DiagnosticReport search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── Encounter Resource ───────────────────────────────────────────────────

    /// <summary>
    /// FHIR Encounter search — GET /api/fhir/Encounter?patient={patientId}
    /// Maps VistA VISIT file (#9000010) to FHIR Encounter.
    /// </summary>
    [HttpGet("Encounter")]
    public async Task<IActionResult> SearchEncounters([FromQuery] string patient)
    {
        try
        {
            List<PceVisitEntry> visits = await W(patient).GetEncounterListAsync(50);

            var entries = visits.Select(v => new
            {
                fullUrl = $"Encounter/{v.VisitId}",
                resource = new
                {
                    resourceType = "Encounter",
                    id = v.VisitId,
                    status = MapEncounterStatus(v.Status),
                    @class = new
                    {
                        system = "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                        code = MapServiceCategory(v.ServiceCategory),
                        display = MapServiceCategoryDisplay(v.ServiceCategory)
                    },
                    type = !string.IsNullOrEmpty(v.LocationName) ? new[]
                    {
                        new { text = v.LocationName }
                    } : null,
                    subject = new { reference = $"Patient/{patient}" },
                    period = new
                    {
                        start = v.VisitDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    },
                    location = !string.IsNullOrEmpty(v.LocationName) ? new[]
                    {
                        new { location = new { display = v.LocationName } }
                    } : null
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Encounter search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── Appointment Resource ─────────────────────────────────────────────────

    /// <summary>
    /// FHIR Appointment search — GET /api/fhir/Appointment?patient={patientId}
    /// Maps VistA APPOINTMENT file (#44.003) to FHIR Appointment.
    /// </summary>
    [HttpGet("Appointment")]
    public async Task<IActionResult> SearchAppointments([FromQuery] string patient)
    {
        try
        {
            List<AppointmentEntry> appointments = await W(patient).GetAllAppointmentsAsync(50);

            var entries = appointments.Select(a => new
            {
                fullUrl = $"Appointment/{a.AppointmentId}",
                resource = new
                {
                    resourceType = "Appointment",
                    id = a.AppointmentId,
                    status = MapAppointmentStatus(a.Status),
                    start = a.AppointmentDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    end = a.AppointmentDateTime.AddMinutes(a.DurationMinutes > 0 ? a.DurationMinutes : 30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    minutesDuration = a.DurationMinutes > 0 ? a.DurationMinutes : 30,
                    description = a.ClinicName,
                    participant = new[]
                    {
                        new
                        {
                            actor = new { reference = $"Patient/{patient}" },
                            status = "accepted"
                        }
                    }
                }
            }).ToList<object>();

            return Ok(FhirBundle("searchset", entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FHIR Appointment search error for {Patient}", patient);
            return StatusCode(500, FhirOperationOutcome("exception", "Internal server error"));
        }
    }

    // ─── FHIR Helpers ─────────────────────────────────────────────────────────

    private static object MapPatientToFhir(string patientId, PatientState s)
    {
        // Parse VistA "LAST,FIRST MI" name format
        string family = s.Name;
        string given = string.Empty;
        if (s.Name.Contains(','))
        {
            string[] parts = s.Name.Split(',', 2);
            family = parts[0].Trim();
            given = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        return new
        {
            resourceType = "Patient",
            id = patientId,
            meta = new { profile = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient" } },
            identifier = new[]
            {
                new
                {
                    system = "http://va.gov/systems/mpiICN",
                    value = s.Icn ?? patientId
                },
                !string.IsNullOrEmpty(s.SocialSecurityNumber) ? new
                {
                    system = "http://hl7.org/fhir/sid/us-ssn",
                    value = s.SocialSecurityNumber
                } : null
            }.Where(x => x != null).ToArray(),
            name = new[]
            {
                new
                {
                    use = "official",
                    family,
                    given = !string.IsNullOrEmpty(given) ? new[] { given } : Array.Empty<string>()
                }
            },
            gender = s.Sex switch { "M" => "male", "F" => "female", _ => "unknown" },
            birthDate = s.DateOfBirth?.ToString("yyyy-MM-dd"),
            deceasedDateTime = s.DateOfDeath?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            address = !string.IsNullOrEmpty(s.StreetAddress1) ? new[]
            {
                new
                {
                    line = new[] { s.StreetAddress1, s.StreetAddress2, s.StreetAddress3 }.Where(l => !string.IsNullOrEmpty(l)).ToArray(),
                    city = s.City,
                    state = s.State,
                    postalCode = s.ZipCode
                }
            } : null,
            telecom = new[]
            {
                !string.IsNullOrEmpty(s.PhoneNumberResidence) ? new { system = "phone", value = s.PhoneNumberResidence, use = (string?)"home" } : null,
                !string.IsNullOrEmpty(s.PhoneNumberWork) ? new { system = "phone", value = s.PhoneNumberWork, use = (string?)"work" } : null,
                !string.IsNullOrEmpty(s.Email) ? new { system = "email", value = s.Email, use = (string?)null } : null
            }.Where(t => t != null).ToArray(),
            maritalStatus = !string.IsNullOrEmpty(s.MaritalStatus) ? new { text = s.MaritalStatus } : null,
            extension = s.Veteran == "Y" ? new[]
            {
                new
                {
                    url = "http://va.gov/fhir/StructureDefinition/veteran-status",
                    valueBoolean = true
                }
            } : null
        };
    }

    private static object FhirBundle(string type, List<object> entries) => new
    {
        resourceType = "Bundle",
        type,
        total = entries.Count,
        entry = entries
    };

    private static object FhirOperationOutcome(string code, string message) => new
    {
        resourceType = "OperationOutcome",
        issue = new[]
        {
            new { severity = "error", code, diagnostics = message }
        }
    };

    private static string MapAllergyCriticality(string? severity) => severity?.ToUpperInvariant() switch
    {
        "SEVERE" => "high",
        "MODERATE" => "low",
        "MILD" => "low",
        _ => "unable-to-assess"
    };

    private static string MapAllergyReactionSeverity(string? severity) => severity?.ToUpperInvariant() switch
    {
        "SEVERE" => "severe",
        "MODERATE" => "moderate",
        "MILD" => "mild",
        _ => "moderate"
    };

    private static object MapVitalToLoinc(string vitalType) => vitalType.ToUpperInvariant() switch
    {
        "TEMPERATURE" => new { system = "http://loinc.org", code = "8310-5", display = "Body temperature" },
        "PULSE" => new { system = "http://loinc.org", code = "8867-4", display = "Heart rate" },
        "RESPIRATION" => new { system = "http://loinc.org", code = "9279-1", display = "Respiratory rate" },
        "BLOOD PRESSURE" => new { system = "http://loinc.org", code = "85354-9", display = "Blood pressure panel" },
        "HEIGHT" => new { system = "http://loinc.org", code = "8302-2", display = "Body height" },
        "WEIGHT" => new { system = "http://loinc.org", code = "29463-7", display = "Body weight" },
        "PAIN" => new { system = "http://loinc.org", code = "72514-3", display = "Pain severity" },
        "PULSE OXIMETRY" => new { system = "http://loinc.org", code = "2708-6", display = "Oxygen saturation" },
        _ => new { system = "http://loinc.org", code = "unknown", display = vitalType }
    };

    private static string MapMedStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => "active",
        "PENDING" => "draft",
        "DISCONTINUED" => "stopped",
        "EXPIRED" => "completed",
        "HOLD" => "on-hold",
        _ => "unknown"
    };

    private static string MapEncounterStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "OPEN" => "in-progress",
        "CHECKED OUT" => "finished",
        "CANCELLED" => "cancelled",
        _ => "unknown"
    };

    private static string MapServiceCategory(string? category) => category switch
    {
        "A" => "AMB",
        "T" => "AMB",
        "H" => "IMP",
        "C" => "AMB",
        "E" => "EMER",
        _ => "AMB"
    };

    private static string MapServiceCategoryDisplay(string? category) => category switch
    {
        "A" => "ambulatory",
        "T" => "ambulatory",
        "H" => "inpatient encounter",
        "C" => "ambulatory",
        "E" => "emergency",
        _ => "ambulatory"
    };

    private static string MapAppointmentStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "SCHEDULED" => "booked",
        "CHECKED IN" or "CHECKEDIN" => "arrived",
        "CHECKED OUT" or "CHECKEDOUT" => "fulfilled",
        "CANCELLED" => "cancelled",
        "NO SHOW" or "NOSHOW" => "noshow",
        _ => "proposed"
    };
}
