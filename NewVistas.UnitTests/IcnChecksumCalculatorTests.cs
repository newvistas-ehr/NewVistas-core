// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests for the deterministic ICN checksum helper. The algorithm itself is a
/// stand-in (see <see cref="IcnChecksumCalculator"/> remarks) — these tests
/// pin its observable contract so a future swap to an authoritative VA
/// algorithm would surface as a deliberate test update.
/// </summary>
[TestFixture]
public class IcnChecksumCalculatorTests
{
    [Test]
    public void Compute_IsDeterministic()
    {
        string a = IcnChecksumCalculator.Compute("5180003421");
        string b = IcnChecksumCalculator.Compute("5180003421");
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void Compute_ReturnsExactlySixDigits()
    {
        string sum = IcnChecksumCalculator.Compute("0000000001");
        Assert.That(sum, Has.Length.EqualTo(6));
        Assert.That(sum, Does.Match("^[0-9]{6}$"));
    }

    [Test]
    public void Compute_DiffersForDifferentSequence()
    {
        string a = IcnChecksumCalculator.Compute("5180000001");
        string b = IcnChecksumCalculator.Compute("5180000002");
        Assert.That(b, Is.Not.EqualTo(a));
    }

    [Test]
    public void Compute_DiffersForDifferentPrefix()
    {
        string a = IcnChecksumCalculator.Compute("5180000001");
        string b = IcnChecksumCalculator.Compute("6620000001");
        Assert.That(b, Is.Not.EqualTo(a));
    }

    [Test]
    public void Compute_RejectsWrongLengthInput()
    {
        Assert.That(() => IcnChecksumCalculator.Compute(""), Throws.ArgumentException);
        Assert.That(() => IcnChecksumCalculator.Compute("12345"), Throws.ArgumentException);
        Assert.That(() => IcnChecksumCalculator.Compute("12345678901"), Throws.ArgumentException);
    }

    [Test]
    public void Compute_RejectsNonDigitInput()
    {
        Assert.That(() => IcnChecksumCalculator.Compute("51800034ZZ"), Throws.ArgumentException);
        Assert.That(() => IcnChecksumCalculator.Compute("ABCDEFGHIJ"), Throws.ArgumentException);
    }

    [Test]
    public void Compute_RejectsNullInput()
    {
        Assert.That(() => IcnChecksumCalculator.Compute(null!), Throws.ArgumentNullException);
    }
}
