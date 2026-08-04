using Tronox.Application.Documentos;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS (sin object storage ni EF) de las reglas de documentos (RQ04): validacion de binario
/// (extension/tamano), formato, content-type, hash de integridad, OCR inicial, herencia de
/// clasificacion "solo elevar" y obligatoriedad de metadatos.
/// </summary>
public class DocumentoRulesTests
{
    [Fact]
    public void ValidateBinario_RechazaVacioYExcesoYExtension()
    {
        Assert.NotNull(DocumentoRules.ValidateBinario("x.pdf", 0));
        Assert.NotNull(DocumentoRules.ValidateBinario("x.pdf", DocumentoRules.MaxBytes + 1));
        Assert.NotNull(DocumentoRules.ValidateBinario("x.exe", 100));
        Assert.NotNull(DocumentoRules.ValidateBinario("sinextension", 100));
        Assert.Null(DocumentoRules.ValidateBinario("contrato.PDF", 100));
        Assert.Null(DocumentoRules.ValidateBinario("hoja.xlsx", 100));
    }

    [Fact]
    public void Formato_YExtension_Normalizan()
    {
        Assert.Equal("pdf", DocumentoRules.Extension("Documento.PDF"));
        Assert.Equal("PDF", DocumentoRules.Formato("Documento.pdf"));
        Assert.Equal("", DocumentoRules.Extension("sinpunto"));
    }

    [Fact]
    public void ContentType_MapeaConocidos()
    {
        Assert.Equal("application/pdf", DocumentoRules.ContentType("a.pdf"));
        Assert.Equal("image/png", DocumentoRules.ContentType("a.png"));
        Assert.Equal("application/xml", DocumentoRules.ContentType("a.xml"));
        Assert.Equal("application/octet-stream", DocumentoRules.ContentType("a.desconocido"));
    }

    [Fact]
    public void HashSha256_EsDeterministaYHex()
    {
        var a = DocumentoRules.HashSha256("hola"u8.ToArray());
        var b = DocumentoRules.HashSha256("hola"u8.ToArray());
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Equal(a, a.ToLowerInvariant());
    }

    [Fact]
    public void OcrInicial_SoloParaPdfEImagenes()
    {
        Assert.Equal(OcrEstadoDocumento.Pendiente, DocumentoRules.OcrInicial("a.pdf"));
        Assert.Equal(OcrEstadoDocumento.Pendiente, DocumentoRules.OcrInicial("a.jpg"));
        Assert.Equal(OcrEstadoDocumento.NoAplica, DocumentoRules.OcrInicial("a.docx"));
        Assert.Equal(OcrEstadoDocumento.NoAplica, DocumentoRules.OcrInicial("a.xml"));
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(2, 1, false)]
    public void PuedeElevar_SoloIgualOMayor(int heredado, int elegido, bool esperado)
        => Assert.Equal(esperado, DocumentoRules.PuedeElevar(heredado, elegido));

    [Fact]
    public void ValidateMetadatosObligatorios_DetectaFaltante()
    {
        var defs = new (long, string, bool)[] { (5, "Numero de acta", true) };
        var error = DocumentoRules.ValidateMetadatosObligatorios(defs, new Dictionary<long, string?>());
        Assert.NotNull(error);
        Assert.Contains("Numero de acta", error);
    }
}
