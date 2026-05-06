// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Lab Shipping Manifest grain based on VistA
/// LAB SHIPPING MANIFEST file (#62.8). Tracks batched specimens
/// shipped to reference laboratories.
/// </summary>
[GenerateSerializer]
public class ShippingManifestState
{
    /// <summary>
    /// Manifest unique identifier
    /// </summary>
    [Id(0)]
    public string ManifestId { get; set; } = string.Empty;

    /// <summary>
    /// Shipping configuration ID (reference to #62.9)
    /// </summary>
    [Id(1)]
    public string ShippingConfigId { get; set; } = string.Empty;

    /// <summary>
    /// Reference lab destination name
    /// </summary>
    [Id(2)]
    public string DestinationLab { get; set; } = string.Empty;

    /// <summary>
    /// Manifest status — OPEN, SHIPPED, RECEIVED, CANCELLED
    /// </summary>
    [Id(3)]
    public ShippingManifestStatus Status { get; set; } = ShippingManifestStatus.OPEN;

    /// <summary>
    /// Specimens/tests included in this manifest
    /// </summary>
    [Id(4)]
    public List<ManifestSpecimen> Specimens { get; set; } = new();

    /// <summary>
    /// Tracking number from carrier
    /// </summary>
    [Id(5)]
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// Shipping method (e.g., "FedEx Priority", "Courier")
    /// </summary>
    [Id(6)]
    public string? ShippingMethod { get; set; }

    /// <summary>
    /// Shipping conditions (e.g., "Ambient", "Frozen", "Refrigerated")
    /// </summary>
    [Id(7)]
    public string? ShippingConditions { get; set; }

    /// <summary>
    /// Container type (e.g., "Styrofoam Box", "Biohazard Bag")
    /// </summary>
    [Id(8)]
    public string? ContainerType { get; set; }

    /// <summary>
    /// Date manifest was created
    /// </summary>
    [Id(9)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date manifest was shipped
    /// </summary>
    [Id(10)]
    public DateTime? ShippedDate { get; set; }

    /// <summary>
    /// Date specimens were received by reference lab
    /// </summary>
    [Id(11)]
    public DateTime? ReceivedDate { get; set; }

    /// <summary>
    /// User who created/shipped the manifest
    /// </summary>
    [Id(12)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A specimen entry within a shipping manifest.
/// Mirrors tests in VistA ^LAHM(62.8,manifestIEN,tests).
/// </summary>
[GenerateSerializer]
public class ManifestSpecimen
{
    /// <summary>
    /// Accession/specimen unique ID
    /// </summary>
    [Id(0)]
    public string SpecimenId { get; set; } = string.Empty;

    /// <summary>
    /// Patient ID this specimen belongs to
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Test name ordered
    /// </summary>
    [Id(2)]
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// LOINC code for the test
    /// </summary>
    [Id(3)]
    public string? LoincCode { get; set; }

    /// <summary>
    /// Specimen type (e.g., "Blood", "Serum")
    /// </summary>
    [Id(4)]
    public string? SpecimenType { get; set; }

    /// <summary>
    /// Collection date/time
    /// </summary>
    [Id(5)]
    public DateTime? CollectionDate { get; set; }

    /// <summary>
    /// Whether this specimen was removed from the manifest
    /// Mirrors VistA field .08 = 0 for removed tests.
    /// </summary>
    [Id(6)]
    public bool IsRemoved { get; set; }
}

/// <summary>
/// Shipping manifest lifecycle status.
/// </summary>
[GenerateSerializer]
public enum ShippingManifestStatus
{
    OPEN,
    SHIPPED,
    RECEIVED,
    CANCELLED
}

/// <summary>
/// State for the Lab Shipping Configuration grain based on VistA
/// LAB SHIPPING CONFIGURATION file (#62.9).
/// </summary>
[GenerateSerializer]
public class ShippingConfigState
{
    /// <summary>
    /// Configuration unique identifier
    /// </summary>
    [Id(0)]
    public string ConfigId { get; set; } = string.Empty;

    /// <summary>
    /// Reference lab name
    /// </summary>
    [Id(1)]
    public string LabName { get; set; } = string.Empty;

    /// <summary>
    /// Lab address
    /// </summary>
    [Id(2)]
    public string? Address { get; set; }

    /// <summary>
    /// Lab contact phone
    /// </summary>
    [Id(3)]
    public string? Phone { get; set; }

    /// <summary>
    /// Default shipping method
    /// </summary>
    [Id(4)]
    public string? DefaultShippingMethod { get; set; }

    /// <summary>
    /// Default shipping conditions
    /// </summary>
    [Id(5)]
    public string? DefaultConditions { get; set; }

    /// <summary>
    /// Default container type
    /// </summary>
    [Id(6)]
    public string? DefaultContainerType { get; set; }

    /// <summary>
    /// Whether this configuration is active
    /// </summary>
    [Id(7)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tests this reference lab accepts (LOINC codes)
    /// </summary>
    [Id(8)]
    public List<string> AcceptedTests { get; set; } = new();

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
