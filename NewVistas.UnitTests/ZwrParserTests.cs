// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

[TestFixture]
public class ZwrParserTests
{
    // ── ParseLine ──────────────────────────────────────────────────────────────

    [Test]
    public void ParseLine_DptZeroNode_ExtractsDemographics()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^DPT(1,0)=\"DOE,JOHN^M^2800101^000123456P\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("DPT"));
        Assert.That(record.FileNumber, Is.Null);
        Assert.That(record.Ien, Is.EqualTo(1));
        Assert.That(record.Subscripts, Has.Count.EqualTo(1));
        Assert.That(record.Subscripts[0], Is.EqualTo("0"));
        Assert.That(ZwrParser.Piece(record.Value, 1), Is.EqualTo("DOE,JOHN"));
        Assert.That(ZwrParser.Piece(record.Value, 2), Is.EqualTo("M"));
        Assert.That(ZwrParser.Piece(record.Value, 3), Is.EqualTo("2800101"));
        Assert.That(ZwrParser.Piece(record.Value, 4), Is.EqualTo("000123456P"));
    }

    [Test]
    public void ParseLine_DptAddressNode_ExtractsAddress()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^DPT(42,.11)=\"123 MAIN ST^^ANYTOWN^VA^24060\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Ien, Is.EqualTo(42));
        Assert.That(record.Subscripts[0], Is.EqualTo(".11"));
        Assert.That(ZwrParser.Piece(record.Value, 1), Is.EqualTo("123 MAIN ST"));
        Assert.That(ZwrParser.Piece(record.Value, 2), Is.Null); // empty piece
        Assert.That(ZwrParser.Piece(record.Value, 3), Is.EqualTo("ANYTOWN"));
        Assert.That(ZwrParser.Piece(record.Value, 4), Is.EqualTo("VA"));
        Assert.That(ZwrParser.Piece(record.Value, 5), Is.EqualTo("24060"));
    }

    [Test]
    public void ParseLine_GmrAllergyNode_ExtractsFileNumber()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^GMR(120.8,5,0)=\"PENICILLIN^Drug^3;PSDRUG(^ALLERGY\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("GMR"));
        Assert.That(record.FileNumber, Is.EqualTo("120.8"));
        Assert.That(record.Ien, Is.EqualTo(5));
        Assert.That(record.Subscripts[0], Is.EqualTo("0"));
        Assert.That(ZwrParser.Piece(record.Value, 1), Is.EqualTo("PENICILLIN"));
        Assert.That(ZwrParser.Piece(record.Value, 2), Is.EqualTo("Drug"));
    }

    [Test]
    public void ParseLine_LabChemistry_HandlesNestedSubscripts()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^LR(63,1,\"CH\",3250101,1)=\"^^^WBC^4.5^K/cmm^4.5^11.0^\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("LR"));
        Assert.That(record.FileNumber, Is.EqualTo("63"));
        Assert.That(record.Ien, Is.EqualTo(1));
        Assert.That(record.Subscripts, Has.Count.EqualTo(3));
        Assert.That(record.Subscripts[0], Is.EqualTo("CH"));
        Assert.That(record.Subscripts[1], Is.EqualTo("3250101"));
        Assert.That(record.Subscripts[2], Is.EqualTo("1"));
        Assert.That(ZwrParser.Piece(record.Value, 4), Is.EqualTo("WBC"));
        Assert.That(ZwrParser.Piece(record.Value, 5), Is.EqualTo("4.5"));
        Assert.That(ZwrParser.Piece(record.Value, 6), Is.EqualTo("K/cmm"));
    }

    [Test]
    public void ParseLine_OrderNode_ExtractsFileNumber()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^OR(100,10,0)=\"ACTIVE^1^200^\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("OR"));
        Assert.That(record.FileNumber, Is.EqualTo("100"));
        Assert.That(record.Ien, Is.EqualTo(10));
        Assert.That(ZwrParser.Piece(record.Value, 1), Is.EqualTo("ACTIVE"));
    }

    [Test]
    public void ParseLine_SurgeryNode_NoFileNumber()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^SRF(7,0)=\"1^APPENDECTOMY^3250115^\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("SRF"));
        Assert.That(record.FileNumber, Is.Null);
        Assert.That(record.Ien, Is.EqualTo(7));
        Assert.That(ZwrParser.Piece(record.Value, 2), Is.EqualTo("APPENDECTOMY"));
    }

    [Test]
    public void ParseLine_ProblemNode_NoFileNumber()
    {
        ZwrRecord? record = ZwrParser.ParseLine(
            "^AUPNPROB(3,0)=\"TYPE 2 DIABETES^CHRONIC^3200601^ACTIVE^1\"");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Global, Is.EqualTo("AUPNPROB"));
        Assert.That(record.FileNumber, Is.Null);
        Assert.That(record.Ien, Is.EqualTo(3));
        Assert.That(ZwrParser.Piece(record.Value, 1), Is.EqualTo("TYPE 2 DIABETES"));
        Assert.That(ZwrParser.Piece(record.Value, 4), Is.EqualTo("ACTIVE"));
    }

    [Test]
    public void ParseLine_EmptyOrBlank_ReturnsNull()
    {
        Assert.That(ZwrParser.ParseLine(""), Is.Null);
        Assert.That(ZwrParser.ParseLine("   "), Is.Null);
        Assert.That(ZwrParser.ParseLine("; this is a comment"), Is.Null);
    }

    [Test]
    public void ParseLine_InvalidFormat_ReturnsNull()
    {
        Assert.That(ZwrParser.ParseLine("not a zwr line"), Is.Null);
        Assert.That(ZwrParser.ParseLine("GLOBAL(1,0)=\"value\""), Is.Null); // missing ^
    }

    // ── ParseFmDate ────────────────────────────────────────────────────────────

    [Test]
    public void ParseFmDate_StandardDate_ReturnsCorrectDateTime()
    {
        DateTime? result = ZwrParser.ParseFmDate("3250101");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Year, Is.EqualTo(2025));
        Assert.That(result.Value.Month, Is.EqualTo(1));
        Assert.That(result.Value.Day, Is.EqualTo(1));
    }

    [Test]
    public void ParseFmDate_1980Date_ReturnsCorrectDateTime()
    {
        DateTime? result = ZwrParser.ParseFmDate("2800101");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Year, Is.EqualTo(1980));
        Assert.That(result.Value.Month, Is.EqualTo(1));
        Assert.That(result.Value.Day, Is.EqualTo(1));
    }

    [Test]
    public void ParseFmDate_WithTime_IncludesTimeComponent()
    {
        DateTime? result = ZwrParser.ParseFmDate("3250315.143022");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Year, Is.EqualTo(2025));
        Assert.That(result.Value.Month, Is.EqualTo(3));
        Assert.That(result.Value.Day, Is.EqualTo(15));
        Assert.That(result.Value.Hour, Is.EqualTo(14));
        Assert.That(result.Value.Minute, Is.EqualTo(30));
        Assert.That(result.Value.Second, Is.EqualTo(22));
    }

    [Test]
    public void ParseFmDate_WithPartialTime_HandlesGracefully()
    {
        DateTime? result = ZwrParser.ParseFmDate("3250315.14");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Hour, Is.EqualTo(14));
        Assert.That(result.Value.Minute, Is.EqualTo(0));
        Assert.That(result.Value.Second, Is.EqualTo(0));
    }

    [Test]
    public void ParseFmDate_NullOrEmpty_ReturnsNull()
    {
        Assert.That(ZwrParser.ParseFmDate(null), Is.Null);
        Assert.That(ZwrParser.ParseFmDate(""), Is.Null);
        Assert.That(ZwrParser.ParseFmDate("   "), Is.Null);
    }

    [Test]
    public void ParseFmDate_TooShort_ReturnsNull()
    {
        Assert.That(ZwrParser.ParseFmDate("325"), Is.Null);
        Assert.That(ZwrParser.ParseFmDate("32501"), Is.Null);
    }

    // ── Piece ──────────────────────────────────────────────────────────────────

    [Test]
    public void Piece_ValidIndex_ReturnsCorrectPiece()
    {
        string value = "DOE,JOHN^M^2800101^000123456P";

        Assert.That(ZwrParser.Piece(value, 1), Is.EqualTo("DOE,JOHN"));
        Assert.That(ZwrParser.Piece(value, 2), Is.EqualTo("M"));
        Assert.That(ZwrParser.Piece(value, 3), Is.EqualTo("2800101"));
        Assert.That(ZwrParser.Piece(value, 4), Is.EqualTo("000123456P"));
    }

    [Test]
    public void Piece_OutOfRange_ReturnsNull()
    {
        Assert.That(ZwrParser.Piece("A^B^C", 4), Is.Null);
        Assert.That(ZwrParser.Piece("A^B^C", 0), Is.Null);
        Assert.That(ZwrParser.Piece("A^B^C", -1), Is.Null);
    }

    [Test]
    public void Piece_EmptyPiece_ReturnsNull()
    {
        // ^^ produces empty string between delimiters — Piece returns null for empty
        Assert.That(ZwrParser.Piece("A^^C", 2), Is.Null);
    }

    [Test]
    public void Piece_NullInput_ReturnsNull()
    {
        Assert.That(ZwrParser.Piece(null, 1), Is.Null);
        Assert.That(ZwrParser.Piece("", 1), Is.Null);
    }

    [Test]
    public void Piece_SingleValue_ReturnsValue()
    {
        Assert.That(ZwrParser.Piece("ONLY", 1), Is.EqualTo("ONLY"));
    }

    // ── ParseFile ──────────────────────────────────────────────────────────────

    [Test]
    public void ParseFile_MultiplePatients_GroupsByIen()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "^DPT(1,0)=\"DOE,JOHN^M^2800101^000111111P\"",
                "^DPT(1,.11)=\"123 MAIN ST^^ANYTOWN^VA^24060\"",
                "^DPT(2,0)=\"SMITH,JANE^F^2900202^000222222P\"",
                "^DPT(2,.11)=\"456 OAK AVE^^ELSEWHERE^MD^20001\""
            });

            var result = ZwrParser.ParseFile(tempFile);

            Assert.That(result, Has.Count.EqualTo(2)); // 2 patients
            Assert.That(result.ContainsKey(("DPT", null, 1)), Is.True);
            Assert.That(result.ContainsKey(("DPT", null, 2)), Is.True);
            Assert.That(result[("DPT", null, 1)], Has.Count.EqualTo(2)); // 0-node + .11
            Assert.That(result[("DPT", null, 2)], Has.Count.EqualTo(2));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void ParseFile_MixedGlobals_SeparatesByGlobal()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "^DPT(1,0)=\"DOE,JOHN^M^2800101^000111111P\"",
                "^GMR(120.8,1,0)=\"PENICILLIN^Drug^^\"",
                "^AUPNPROB(1,0)=\"DIABETES^CHRONIC^3200601^ACTIVE\""
            });

            var result = ZwrParser.ParseFile(tempFile);

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.ContainsKey(("DPT", null, 1)), Is.True);
            Assert.That(result.ContainsKey(("GMR", "120.8", 1)), Is.True);
            Assert.That(result.ContainsKey(("AUPNPROB", null, 1)), Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // ── IenMap ─────────────────────────────────────────────────────────────────

    [Test]
    public void IenMap_GetOrCreateKey_ReturnsDeterministicKey()
    {
        var map = new NewVistas.Abstractions.Importers.IenMap();

        string key1 = map.GetOrCreateKey("DPT", 42, "PATIENT");
        string key2 = map.GetOrCreateKey("DPT", 42, "PATIENT");

        Assert.That(key1, Is.EqualTo("P42"));
        Assert.That(key2, Is.EqualTo(key1)); // same key on second call
    }

    [Test]
    public void IenMap_TryGetKey_ReturnsNullForUnknown()
    {
        var map = new NewVistas.Abstractions.Importers.IenMap();

        Assert.That(map.TryGetKey("DPT", 999), Is.Null);
    }

    [Test]
    public void IenMap_TryGetKey_ReturnsKeyAfterCreate()
    {
        var map = new NewVistas.Abstractions.Importers.IenMap();
        map.GetOrCreateKey("DPT", 1, "PATIENT");

        Assert.That(map.TryGetKey("DPT", 1), Is.EqualTo("P1"));
    }
}
