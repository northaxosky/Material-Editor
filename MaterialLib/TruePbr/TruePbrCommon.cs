using System.Runtime.Serialization;

namespace MaterialLib
{
    [DataContract]
    public sealed class TruePbrCommon
    {
        private float[] _uvOffset = [];
        private float[] _uvScale = [];
        private float[] _emissiveColor = [];

        public TruePbrCommon()
        {
            SetDefaults();
        }

        [OnDeserializing]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used by JSON serialization.")]
        private void OnDeserializing(StreamingContext c)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            TileU = true;
            TileV = true;
            UvOffset = [0, 0];
            UvScale = [1, 1];
            Alpha = 1.0f;
            AlphaBlendMode = BaseMaterialFile.AlphaBlendModeType.None;
            AlphaTestRef = 128;
            EmissiveColor = [1, 1, 1];
            EmissiveScale = 1.0f;
        }

        [DataMember(Name = "tileU", Order = 1)]
        public bool TileU { get; set; }

        [DataMember(Name = "tileV", Order = 2)]
        public bool TileV { get; set; }

        [DataMember(Name = "uvOffset", Order = 3)]
        public float[] UvOffset
        {
            get => _uvOffset;
            set => _uvOffset = Exactly(value, 2, "uvOffset");
        }

        [DataMember(Name = "uvScale", Order = 4)]
        public float[] UvScale
        {
            get => _uvScale;
            set => _uvScale = Exactly(value, 2, "uvScale");
        }

        [DataMember(Name = "alpha", Order = 5)]
        public float Alpha { get; set; }

        public BaseMaterialFile.AlphaBlendModeType AlphaBlendMode { get; set; }

        [DataMember(Name = "alphaBlendMode", Order = 6)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used by JSON serialization.")]
        private string AlphaBlendModeString
        {
            get { return BaseMaterialFile.AlphaBlendModeName(AlphaBlendMode); }
            set
            {
                AlphaBlendMode = Enum.TryParse(value, true, out BaseMaterialFile.AlphaBlendModeType mode) ? mode : BaseMaterialFile.AlphaBlendModeType.None;
            }
        }

        [DataMember(Name = "alphaTest", Order = 7)]
        public bool AlphaTest { get; set; }

        [DataMember(Name = "alphaTestRef", Order = 8)]
        public byte AlphaTestRef { get; set; }

        [DataMember(Name = "refraction", Order = 9)]
        public bool Refraction { get; set; }

        [DataMember(Name = "refractionFalloff", Order = 10)]
        public bool RefractionFalloff { get; set; }

        [DataMember(Name = "refractionPower", Order = 11)]
        public float RefractionPower { get; set; }

        [DataMember(Name = "modelSpaceNormals", Order = 12)]
        public bool ModelSpaceNormals { get; set; }

        [DataMember(Name = "emissive", Order = 13)]
        public bool Emissive { get; set; }

        // Linear floats; may exceed 1.
        [DataMember(Name = "emissiveColor", Order = 14)]
        public float[] EmissiveColor
        {
            get => _emissiveColor;
            set => _emissiveColor = Exactly(value, 3, "emissiveColor");
        }

        [DataMember(Name = "emissiveScale", Order = 15)]
        public float EmissiveScale { get; set; }

        [DataMember(Name = "externalEmittance", Order = 16)]
        public bool ExternalEmittance { get; set; }

        public static TruePbrCommon Read(BinaryReader input)
        {
            var common = new TruePbrCommon();

            BaseMaterialFile.UnpackTileFlags(input.ReadUInt32(), out var tileU, out var tileV);
            common.TileU = tileU;
            common.TileV = tileV;
            common.UvOffset = [input.ReadSingle(), input.ReadSingle()];
            common.UvScale = [input.ReadSingle(), input.ReadSingle()];

            common.Alpha = input.ReadSingle();
            common.AlphaBlendMode = BaseMaterialFile.ReadAlphaBlendMode(input);
            common.AlphaTestRef = input.ReadByte();
            common.AlphaTest = input.ReadBoolean();

            common.Refraction = input.ReadBoolean();
            common.RefractionFalloff = input.ReadBoolean();
            common.RefractionPower = input.ReadSingle();

            common.ModelSpaceNormals = input.ReadBoolean();
            common.Emissive = input.ReadBoolean();
            common.EmissiveColor = [input.ReadSingle(), input.ReadSingle(), input.ReadSingle()];
            common.EmissiveScale = input.ReadSingle();
            common.ExternalEmittance = input.ReadBoolean();
            return common;
        }

        public void Write(BinaryWriter output)
        {
            output.Write(BaseMaterialFile.PackTileFlags(TileU, TileV));

            foreach (var value in UvOffset) output.Write(value);
            foreach (var value in UvScale) output.Write(value);

            output.Write(Alpha);

            BaseMaterialFile.WriteAlphaBlendMode(output, AlphaBlendMode);

            output.Write(AlphaTestRef);
            output.Write(AlphaTest);

            output.Write(Refraction);
            output.Write(RefractionFalloff);
            output.Write(RefractionPower);

            output.Write(ModelSpaceNormals);
            output.Write(Emissive);
            foreach (var value in EmissiveColor) output.Write(value);
            output.Write(EmissiveScale);
            output.Write(ExternalEmittance);
        }

        private static float[] Exactly(float[] value, int count, string name)
        {
            if (value == null || value.Length != count)
                throw new SerializationException($"'{name}' needs {count} values.");

            return value;
        }
    }
}
