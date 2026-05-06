# Patient Grain Implementation

## Overview
This implementation provides Orleans-based grain components for the VistA PATIENT file (#2), which is the core patient record in the Veterans Health Information Systems and Technology Architecture (VistA).

## Components Created

### 1. PatientState.cs (GrainStates/PatientState.cs)
The state class that represents a patient record with the following key fields:

**Demographics:**
- PatientId (IEN)
- Name
- Sex (M/F)
- Date of Birth
- Social Security Number
- Marital Status
- Religious Preference
- Race (multiple)
- Ethnicity (multiple)

**Contact Information:**
- Street Address (3 lines)
- City, State, Zip Code
- Phone Numbers (Residence, Work)
- Email Address

**Emergency Contact:**
- Name
- Relationship
- Phone

**Veteran Information:**
- Veteran Status (Y/N)
- Service Connected Percentage
- Eligibility Codes
- Primary Eligibility Code

**Military Service:**
- Service Entry Date
- Service Separation Date
- Service Branch
- Discharge Type
- Prisoner of War Status

**Clinical:**
- Current Admission
- Room-Bed
- Current Movement
- Appointments (list)
- Date of Death

**System:**
- Created Date
- Last Modified Date
- Is Active

### 2. IPatientGrain.cs (GrainInterfaces/IPatientGrain.cs)
The grain interface that defines all operations available on a patient grain:

**Query Operations:**
- GetPatientAsync() - Returns complete patient state
- GetNameAsync() - Returns patient name
- IsVeteranAsync() - Checks veteran status
- GetAgeAsync() - Calculates age from DOB
- GetAppointmentsAsync() - Returns all appointments

**Update Operations:**
- UpdateDemographicsAsync() - Updates name, sex, DOB, SSN
- UpdateAddressAsync() - Updates street address, city, state, zip
- UpdateContactInfoAsync() - Updates phone numbers and email
- UpdateEmergencyContactAsync() - Updates emergency contact info
- UpdateVeteranInfoAsync() - Updates veteran-related fields
- UpdateMilitaryServiceAsync() - Updates service history
- UpdateMaritalStatusAsync() - Updates marital status
- UpdateReligiousPreferenceAsync() - Updates religious preference
- UpdateBirthPlaceAsync() - Updates birth location
- UpdateCurrentAdmissionAsync() - Updates admission status

**Multi-valued Field Operations:**
- AddRaceAsync() - Adds a race entry
- AddEthnicityAsync() - Adds an ethnicity entry
- AddAppointmentAsync() - Adds an appointment
- RemoveAppointmentAsync() - Removes an appointment

**State Operations:**
- RecordDateOfDeathAsync() - Records death and deactivates
- DeactivateAsync() - Marks patient inactive
- ActivateAsync() - Marks patient active

### 3. PatientGrain.cs (Grains/PatientGrain.cs)
The grain implementation that:
- Inherits from Orleans `Grain` base class
- Implements `IPatientGrain` interface
- Uses persistent state via `IPersistentState<PatientState>`
- Automatically sets PatientId on first activation
- Tracks last modified date on all updates
- Persists state after each modification
- Validates and prevents duplicate entries for collections

## Usage Example

```csharp
// Get a reference to a patient grain
var patientGrain = grainFactory.GetGrain<IPatientGrain>("12345");

// Create/update patient demographics
await patientGrain.UpdateDemographicsAsync(
    name: "DOE,JOHN",
    sex: "M",
    dateOfBirth: new DateTime(1965, 5, 15),
    socialSecurityNumber: "123-45-6789"
);

// Update address
await patientGrain.UpdateAddressAsync(
    streetAddress1: "123 Main St",
    streetAddress2: "Apt 4B",
    streetAddress3: null,
    city: "Washington",
    state: "DC",
    zipCode: "20001"
);

// Update veteran information
await patientGrain.UpdateVeteranInfoAsync(
    veteran: "Y",
    serviceConnectedPercentage: 50,
    eligibilityCode: "SC LESS THAN 50%",
    primaryEligibilityCode: "1"
);

// Add appointments
await patientGrain.AddAppointmentAsync(new DateTime(2026, 2, 15, 10, 0, 0));
await patientGrain.AddAppointmentAsync(new DateTime(2026, 3, 20, 14, 30, 0));

// Retrieve patient
var patient = await patientGrain.GetPatientAsync();
Console.WriteLine($"Patient: {patient.Name}, Age: {await patientGrain.GetAgeAsync()}");

// Check veteran status
if (await patientGrain.IsVeteranAsync())
{
    Console.WriteLine("Patient is a veteran");
}
```

## Orleans Configuration Required

To use these grains, you'll need to configure Orleans in your silo/host:

```csharp
// Add to your host builder
builder.UseOrleans(siloBuilder =>
{
    siloBuilder
        .UseLocalhostClustering()
        .AddMemoryGrainStorage("patientStore");  // or other storage provider
});
```

## VistA Mapping

This implementation maps to the VistA PATIENT file (#2) structure:
- File Number: 2
- Global: ^DPT(
- Common fields from VistA FileMan schema included
- Additional fields can be added as needed

## Notes

1. **Storage:** The grain uses persistent state with the storage name "patientStore". You'll need to configure an appropriate storage provider (Memory, Azure, SQL, etc.).

2. **Key Format:** The grain uses string keys, which should match the VistA Internal Entry Number (IEN) for consistency.

3. **Serialization:** The PatientState class is marked as `[Serializable]` for Orleans serialization.

4. **Validation:** Basic validation is included (e.g., preventing duplicate appointments), but additional business rules may need to be added.

5. **Extensibility:** Additional fields from the VistA PATIENT file can be easily added to the PatientState class and corresponding methods to the interface/grain.

## Next Steps

To integrate with the web API:
1. Add Orleans hosting to NewVistas.WebServer
2. Create a PatientController that uses IPatientGrain
3. Configure storage provider for production use
4. Add data migration from VistA PATIENT file if needed
