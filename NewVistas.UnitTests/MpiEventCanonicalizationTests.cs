// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using NewVistas.Abstractions.Events.Mpi;

namespace NewVistas.UnitTests;

/// <summary>
/// Pin the canonical-form output of MPI federation event records. The hash
/// chain protects this representation; any change to the field set or order
/// must be deliberate (and ride a new <c>Vn</c> version of the record), not
/// accidental.
/// </summary>
[TestFixture]
public class MpiEventCanonicalizationTests
{
    [Test]
    public void RegisteredV1_Canonicalize_PinsFieldOrder()
    {
        var evt = new MpiPatientRegisteredV1
        {
            EventId = "MPI-REG-1",
            PatientId = "0991234567V000001",
            OccurredUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            PatientName = "DOE,JOHN",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1970, 1, 1),
            Sex = "M",
            OriginatingFacilityId = "BEDFORD",
        };
        string canonical = evt.Canonicalize();
        Assert.That(canonical, Is.EqualTo(
            "MpiPatientRegisteredV1|DOE,JOHN|111223333|1970-01-01T00:00:00.0000000|M|BEDFORD"));
    }

    [Test]
    public void RegisteredV1_Canonicalize_NullDateOfBirthAndSex_OmitsCleanly()
    {
        var evt = new MpiPatientRegisteredV1
        {
            EventId = "MPI-REG-2",
            PatientId = "0991234567V000002",
            OccurredUtc = DateTime.UtcNow,
            PatientName = "DOE,JANE",
            Ssn = "222334444",
            DateOfBirth = null,
            Sex = null,
            OriginatingFacilityId = "SF",
        };
        string canonical = evt.Canonicalize();
        Assert.That(canonical, Is.EqualTo("MpiPatientRegisteredV1|DOE,JANE|222334444|||SF"));
    }

    [Test]
    public void MergedV1_Canonicalize_PinsFieldOrder()
    {
        var evt = new MpiPatientMergedV1
        {
            EventId = "MPI-MRG-1",
            PatientId = "SOURCE-ICN",
            OccurredUtc = DateTime.UtcNow,
            SourceIcn = "SOURCE-ICN",
            TargetIcn = "TARGET-ICN",
            OriginatingFacilityId = "BEDFORD",
        };
        string canonical = evt.Canonicalize();
        Assert.That(canonical, Is.EqualTo("MpiPatientMergedV1|SOURCE-ICN|TARGET-ICN|BEDFORD"));
    }

    [Test]
    public void RegisteredV1_Domain_IsConstantMpi()
    {
        Assert.That(new MpiPatientRegisteredV1().Domain, Is.EqualTo("MPI"));
    }

    [Test]
    public void MergedV1_Domain_IsConstantMpi()
    {
        Assert.That(new MpiPatientMergedV1().Domain, Is.EqualTo("MPI"));
    }
}
