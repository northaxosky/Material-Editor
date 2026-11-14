using System.Collections;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MaterialLib.Tests
{
    public class TruePbrTests
    {
        private static byte[] Serialize(BaseMaterialFile material)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                material.Serialize(writer);

            return stream.ToArray();
        }

        private static BGSM Deserialize(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            var bgsm = new BGSM();
            bgsm.Deserialize(reader);

            Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
            return bgsm;
        }

        private static TruePbrSection NonDefaultSection()
        {
            var section = new TruePbrSection
            {
                Common = new TruePbrCommon
                {
                    TileU = false,
                    UvOffset = [0.25f, 0.5f],
                    UvScale = [2f, 3f],
                    Alpha = 0.75f,
                    AlphaBlendMode = BaseMaterialFile.AlphaBlendModeType.Additive,
                    AlphaTest = true,
                    AlphaTestRef = 90,
                    Refraction = true,
                    RefractionFalloff = true,
                    RefractionPower = 0.6f,
                    ModelSpaceNormals = true,
                    Emissive = true,
                    EmissiveColor = [4.5f, 0.25f, 2f],
                    EmissiveScale = 12f,
                    ExternalEmittance = true,
                },
                ThinSurfaceOverride = new TruePbrThinSurfaceOverride { SubsurfaceBalance = 0.1f, FrontSpecularWeight = 0.2f, BackSpecularWeight = 0.3f, BackRoughnessScale = 2.5f },
                FuzzOverride = new TruePbrFuzzOverride { Roughness = 0.35f, RoughnessFromDisplacementGreen = true },
            };

            foreach (var slot in TruePbrSchema.Textures)
                section.Textures[slot.Index] = $"pbr\\test\\{slot.Name}.dds";

            foreach (var name in new[] { "twoLayer", "coloredCoat", "interlayerParallax", "coatNormal" })
                section.SetFeature(TruePbrSchema.Features.First(f => f.Name == name), true);

            for (int i = 0; i < section.Parameters.Length; i++)
                section.Parameters[i] = 0.01f + i * 0.037f;

            return section;
        }

        private static BGSM PbrMaterial()
        {
            var bgsm = new BGSM { DiffuseTexture = "vanilla/diffuse.dds", TruePBR = NonDefaultSection() };
            bgsm.UnknownBlocks.Add(new MaterialBlock("ZZZZ", [1, 2, 3, 4, 5]));
            return bgsm;
        }

        private static byte[] SectionBytes(TruePbrSection section)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                MaterialBlock.Write(writer, TruePbrSection.BlockTag, section.Write);

            return stream.ToArray();
        }

        [Fact]
        public void BinaryRoundTrip_PreservesSectionAndUnknownBlocks()
        {
            var original = PbrMaterial();
            var bytes = Serialize(original);

            var loaded = Deserialize(bytes);
            Assert.Equal<uint>(2, loaded.Version);
            Assert.NotNull(loaded.TruePBR);
            Assert.Equal(SectionBytes(original.TruePBR!), SectionBytes(loaded.TruePBR!));
            Assert.Equal(original.UnknownBlocks[0].Payload, Assert.Single(loaded.UnknownBlocks).Payload);
            Assert.Equal(bytes, Serialize(loaded));
        }

        [Fact]
        public void Version0_WithSection_ReadsBack()
        {
            var material = PbrMaterial();
            material.Version = 0;

            var loaded = Deserialize(Serialize(material));
            Assert.Equal(SectionBytes(material.TruePBR!), SectionBytes(loaded.TruePBR!));
        }

        [Fact]
        public void Json_WritesBlendOffAsNone()
        {
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(BGSM)).WriteObject(stream, new BGSM { TruePBR = new TruePbrSection() });
            var json = Encoding.UTF8.GetString(stream.ToArray());

            Assert.Contains("\"eAlphaBlendMode\":\"None\"", json);
            Assert.DoesNotContain("\"Unknown\"", json);
        }

        [Fact]
        public void Version100_BodyIsPlainVersion2()
        {
            var material = PbrMaterial();
            var v100 = Serialize(material);
            Assert.Equal(100u, BitConverter.ToUInt32(v100, 4));

            material.TruePBR = null;
            var v2 = Serialize(material);
            Assert.Equal(2u, BitConverter.ToUInt32(v2, 4));

            Assert.Equal(v2[8..], v100[8..v2.Length]);
            Assert.Equal("CSPB", Encoding.ASCII.GetString(v100, v2.Length, 4));
        }

        [Fact]
        public void Version100_RejectsMissingOrDuplicateBlocks()
        {
            var v2 = Serialize(new BGSM());

            byte[] WithBlocks(params MaterialBlock[] blocks)
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(v2[..4]);
                    writer.Write(100u);
                    writer.Write(v2[8..]);
                    foreach (var block in blocks) block.Write(writer);
                }

                return stream.ToArray();
            }

            var pbr = new MaterialBlock(TruePbrSection.BlockTag, SectionBytes(NonDefaultSection())[8..]);
            var fuzz = new MaterialBlock(TruePbrFuzzOverride.BlockTag, new byte[8]);

            Assert.NotNull(Deserialize(WithBlocks(pbr)).TruePBR);
            Assert.Throws<FormatException>(() => Deserialize(WithBlocks(new MaterialBlock("ZZZZ", [1]))));
            Assert.Throws<FormatException>(() => Deserialize(WithBlocks(pbr, fuzz, fuzz)));
        }

        [Fact]
        public void TruncatedSection_FailsToOpen()
        {
            var bytes = Serialize(PbrMaterial());
            Assert.ThrowsAny<Exception>(() => Deserialize(bytes[..^12]));
        }

        [Fact]
        public void Json_RoundTripsSection()
        {
            var original = PbrMaterial();

            using var stream = new MemoryStream();
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
            {
                var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                new DataContractJsonSerializer(typeof(BGSM), settings).WriteObject(writer, original);
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains("\"TruePBR\"", json);
            Assert.Contains("\"roughnessScale\"", json);

            stream.Position = 0;
            var loaded = (BGSM)new DataContractJsonSerializer(typeof(BGSM)).ReadObject(stream)!;
            Assert.NotNull(loaded.TruePBR);
            Assert.Equal(SectionBytes(original.TruePBR!), SectionBytes(loaded.TruePBR!));
        }

        [Fact]
        public void Json_OmitsSectionWhenAbsent()
        {
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(BGSM)).WriteObject(stream, new BGSM());
            Assert.DoesNotContain("TruePBR", Encoding.UTF8.GetString(stream.ToArray()));
        }

        private static TruePbrSection ValidSection()
        {
            var section = new TruePbrSection();
            section.Textures[0] = "pbr\\a.dds";
            section.Textures[1] = "pbr/a_n.dds";
            section.Textures[4] = "PBR\\a_rmaos.dds";
            return section;
        }

        private static Action<TruePbrSection> Feature(string name)
        {
            return s => s.SetFeature(TruePbrSchema.Features.First(f => f.Name == name), true);
        }

        private static Action<TruePbrSection> Features(params string[] names)
        {
            return s => { foreach (var name in names) Feature(name)(s); };
        }

        private static Action<TruePbrSection> Parameter(string name, float value, int component = 0)
        {
            return s => s.Parameters[TruePbrSchema.Parameters.First(p => p.Name == name).Index + component] = value;
        }

        public static TheoryData<string, Action<TruePbrSection>, string> InvalidSections => new()
        {
            { "colored coat without two layer", Feature("coloredCoat"), "Colored Coat needs Two Layer" },
            { "two layer with subsurface", Features("twoLayer", "subsurface"), "Two Layer cannot be combined with Subsurface" },
            { "flag bit above 9", s => s.FeatureFlags |= 1u << 10, "bits above 9" },
            { "above maximum", Parameter("specularLevel", 1.5f), "Specular Level must be finite" },
            { "missing rmaos", s => s.Textures[4] = "", "RMAOS texture is required" },
            { "coat normal without features1", Features("twoLayer", "coatNormal"), "Features 1 texture is required" },
        };

        [Theory]
        [MemberData(nameof(InvalidSections))]
        public void Validate_RejectsInvalidSection(string _, Action<TruePbrSection> mutate, string expectedError)
        {
            var section = ValidSection();
            mutate(section);

            var result = TruePbrValidator.Validate(section);
            Assert.Contains(result.Errors, e => e.Contains(expectedError));
        }

        [Fact]
        public void Validate_AcceptsValidSection_AndWarnsOutsidePbrFolder()
        {
            var section = ValidSection();
            Assert.Empty(TruePbrValidator.Validate(section).Errors);
            Assert.Empty(TruePbrValidator.Validate(section).Warnings);

            section.Textures[0] = "shared\\vanilla_d.dds";
            var result = TruePbrValidator.Validate(section);
            Assert.Empty(result.Errors);
            Assert.Contains(result.Warnings, w => w.Contains("Base Color"));
        }
    }
}
