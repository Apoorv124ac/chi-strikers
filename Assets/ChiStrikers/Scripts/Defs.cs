using UnityEngine;

namespace ChiStrikers
{
    public enum Role { Keeper, Defender, Playmaker, Winger, Striker }

    /// <summary>The five special-move mechanics. Each hero's special uses one of them with its own look.</summary>
    public enum SpecialType
    {
        PowerShot,   // straight blazing shot that ploughs through players (Agni Strike, Rail Shot)
        AerialShot,  // run up the air, volley down from high (Sky Step, Prism Volley)
        CurveShot,   // ball bends around every defender (Chakra Pass, Vortex Curve)
        Shockwave,   // ground slam knocks opponents flat, then a wall that blocks any shot (Akhada Wall, Titan Slam)
        KeeperSave,  // keeper stops any shot, even specials (Thousand Hands, Iron Gate)
    }

    public enum Hair { Short, Ponytail, Turban, Bald, Bun, Buzz, Mohawk, Visor }

    /// <summary>How a procedural athlete looks.</summary>
    public struct Look
    {
        public Color Skin, Hair, Shirt, Shirt2, Shorts, Socks, Boots, Accent;
        public Hair Style;
        public bool Beard, Moustache, Female, Visor, Wristbands;
        public float Height, Bulk, Age; // Height 1 = 1.80 m; Bulk 1 = athletic; Age 0..1 (greys hair)
        public int Number;
    }

    public sealed class HeroDef
    {
        public string Name, Art, SpecialName, SpecialLine;
        public Role Role;
        public SpecialType Special;
        public Color FxA, FxB;        // special-move colours (core, outer)
        public float Speed = 1f, Shot = 1f, Tackle = 1f, Keeping = 1f;
        public Look Look;
    }

    public sealed class TeamDef
    {
        public string Name, Short;
        public Color Primary, Secondary, Ui;
        public HeroDef[] Heroes; // Keeper, Defender, Playmaker, Winger, Striker
    }

    public static class Teams
    {
        static readonly Color SkinA = Util.Hex("#a8704c"), SkinB = Util.Hex("#d9a07a"), SkinC = Util.Hex("#6e4630"), SkinD = Util.Hex("#f1c9a1");

        public static readonly TeamDef Mohalla = BuildMohalla();
        public static readonly TeamDef Titan = BuildTitan();

        static Look Kit(Look l, int num, bool keeper)
        {
            l.Shirt = keeper ? Util.Hex("#3bb0c8") : Util.Hex("#f28a1e");
            l.Shirt2 = keeper ? Util.Hex("#26324f") : Util.Hex("#f2f2f0");
            l.Shorts = keeper ? Util.Hex("#26324f") : Util.Hex("#26324f");
            l.Socks = Util.Hex("#f2f2f0");
            l.Boots = Util.Hex("#f2f2f0");
            l.Accent = Util.Hex("#ffc93c");
            l.Number = num;
            if (l.Height <= 0) l.Height = 1f;
            if (l.Bulk <= 0) l.Bulk = 1f;
            return l;
        }

        static TeamDef BuildMohalla()
        {
            var t = new TeamDef
            {
                Name = "Mohalla United", Short = "MOH",
                Primary = Util.Hex("#f28a1e"), Secondary = Util.Hex("#26324f"), Ui = Util.Hex("#ffa23c"),
            };
            t.Heroes = new[]
            {
                new HeroDef { Name = "Guru Dada", Art = "Yoga", Role = Role.Keeper, Special = SpecialType.KeeperSave,
                    SpecialName = "THOUSAND HANDS", SpecialLine = "Time slows. A thousand golden hands close the goal.",
                    FxA = Util.Hex("#fff1b0"), FxB = Util.Hex("#ffb52e"), Speed = 0.9f, Keeping = 1.25f,
                    Look = Kit(new Look { Skin = SkinD, Hair = Util.Hex("#e8e4dc"), Style = Hair.Bun, Beard = true, Height = 0.98f, Bulk = 0.85f, Age = 1f }, 1, true) },
                new HeroDef { Name = "Bhola", Art = "Pehlwani wrestling", Role = Role.Defender, Special = SpecialType.Shockwave,
                    SpecialName = "AKHADA WALL", SpecialLine = "He slams the earth. Nobody gets past.",
                    FxA = Util.Hex("#ffe2a8"), FxB = Util.Hex("#c8752e"), Speed = 0.88f, Tackle = 1.6f,
                    Look = Kit(new Look { Skin = SkinC, Hair = Util.Hex("#1a1210"), Style = Hair.Bald, Moustache = true, Height = 1.06f, Bulk = 1.45f }, 4, false) },
                new HeroDef { Name = "Kabir", Art = "Gatka", Role = Role.Playmaker, Special = SpecialType.CurveShot,
                    SpecialName = "CHAKRA STRIKE", SpecialLine = "The ball spins like a chakram and bends around everyone.",
                    FxA = Util.Hex("#d9f6ff"), FxB = Util.Hex("#3aa8ff"), Speed = 1f, Shot = 1.05f,
                    Look = Kit(new Look { Skin = SkinB, Hair = Util.Hex("#1a1414"), Style = Hair.Turban, Beard = true, Accent = Util.Hex("#1d4f9a"), Height = 1.02f }, 8, false) },
                new HeroDef { Name = "Tara", Art = "Mallakhamb", Role = Role.Winger, Special = SpecialType.AerialShot,
                    SpecialName = "SKY STEP", SpecialLine = "Three steps up the air, one volley down.",
                    FxA = Util.Hex("#ffe0f4"), FxB = Util.Hex("#ff5fae"), Speed = 1.15f, Shot = 1f,
                    Look = Kit(new Look { Skin = SkinA, Hair = Util.Hex("#120c0a"), Style = Hair.Ponytail, Female = true, Height = 0.94f, Bulk = 0.85f, Wristbands = true }, 7, false) },
                new HeroDef { Name = "Veer", Art = "Kalaripayattu", Role = Role.Striker, Special = SpecialType.PowerShot,
                    SpecialName = "AGNI STRIKE", SpecialLine = "The ball becomes a comet of fire.",
                    FxA = Util.Hex("#fff0a0"), FxB = Util.Hex("#ff4a12"), Speed = 1.05f, Shot = 1.2f,
                    Look = Kit(new Look { Skin = SkinB, Hair = Util.Hex("#0e0a09"), Style = Hair.Short, Height = 1.01f, Bulk = 1.05f, Wristbands = true }, 10, false) },
            };
            // Kabir's turban colour
            t.Heroes[2].Look.Hair = Util.Hex("#1d4f9a");
            return t;
        }

        static Look TitanKit(Look l, int num, bool keeper)
        {
            l.Shirt = keeper ? Util.Hex("#c5c9cf") : Util.Hex("#26324f");
            l.Shirt2 = keeper ? Util.Hex("#1e1e24") : Util.Hex("#ffc93c");
            l.Shorts = Util.Hex("#1e1e24");
            l.Socks = keeper ? Util.Hex("#c5c9cf") : Util.Hex("#26324f");
            l.Boots = Util.Hex("#1e1e24");
            l.Accent = Util.Hex("#5ff2ff");
            l.Number = num;
            if (l.Height <= 0) l.Height = 1f;
            if (l.Bulk <= 0) l.Bulk = 1f;
            return l;
        }

        static TeamDef BuildTitan()
        {
            var light = SkinD;
            var t = new TeamDef
            {
                Name = "Titan FC", Short = "TTN",
                Primary = Util.Hex("#26324f"), Secondary = Util.Hex("#ffc93c"), Ui = Util.Hex("#7fb6ff"),
            };
            t.Heroes = new[]
            {
                new HeroDef { Name = "Atlas", Art = "Exo-armour", Role = Role.Keeper, Special = SpecialType.KeeperSave,
                    SpecialName = "IRON GATE", SpecialLine = "A wall of hard light seals the goal.",
                    FxA = Util.Hex("#e8ffff"), FxB = Util.Hex("#3fd8ff"), Keeping = 1.15f, Speed = 0.9f,
                    Look = TitanKit(new Look { Skin = light, Hair = Util.Hex("#2a2420"), Style = Hair.Buzz, Visor = true, Height = 1.08f, Bulk = 1.25f }, 1, true) },
                new HeroDef { Name = "Goliath", Art = "Hydraulic limbs", Role = Role.Defender, Special = SpecialType.Shockwave,
                    SpecialName = "TITAN SLAM", SpecialLine = "Steel fists crack the pitch.",
                    FxA = Util.Hex("#fff2c0"), FxB = Util.Hex("#ffb000"), Speed = 0.86f, Tackle = 1.5f,
                    Look = TitanKit(new Look { Skin = SkinC, Hair = Util.Hex("#0e0a09"), Style = Hair.Bald, Beard = true, Height = 1.1f, Bulk = 1.5f }, 5, false) },
                new HeroDef { Name = "Silas", Art = "Gyro-stabilisers", Role = Role.Playmaker, Special = SpecialType.CurveShot,
                    SpecialName = "VORTEX CURVE", SpecialLine = "The ball loops through a spinning gravity well.",
                    FxA = Util.Hex("#f0e0ff"), FxB = Util.Hex("#9a5bff"),
                    Look = TitanKit(new Look { Skin = light, Hair = Util.Hex("#c9b28a"), Style = Hair.Short, Height = 1f }, 6, false) },
                new HeroDef { Name = "Nova", Art = "Jet boots", Role = Role.Winger, Special = SpecialType.AerialShot,
                    SpecialName = "PRISM VOLLEY", SpecialLine = "Jet boots fire, the ball splits into light.",
                    FxA = Util.Hex("#e0fffa"), FxB = Util.Hex("#2fffd0"), Speed = 1.12f,
                    Look = TitanKit(new Look { Skin = SkinD, Hair = Util.Hex("#e8e8f0"), Style = Hair.Mohawk, Female = true, Height = 0.96f, Bulk = 0.85f }, 11, false) },
                new HeroDef { Name = "Rex", Art = "Rail-leg implant", Role = Role.Striker, Special = SpecialType.PowerShot,
                    SpecialName = "RAIL SHOT", SpecialLine = "A magnetic leg fires the ball like a railgun.",
                    FxA = Util.Hex("#e8f6ff"), FxB = Util.Hex("#2f7bff"), Shot = 1.15f, Speed = 1.02f,
                    Look = TitanKit(new Look { Skin = light, Hair = Util.Hex("#1a1a1f"), Style = Hair.Visor, Visor = true, Height = 1.03f, Bulk = 1.1f }, 9, false) },
            };
            return t;
        }
    }
}
