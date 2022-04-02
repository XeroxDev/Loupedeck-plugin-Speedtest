namespace Loupedeck.SpeedtestPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;


    public class ByteSize : ByteBitSizeHelper<ByteSize.SizeUnit>
    {
        static ByteSize()
        {
            UnitIncrement = 1024;
            UnitDisplayChars = 2;
        }
        public static Int64 BytesFromTB(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.TB);
        public static Int64 BytesFromGB(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.GB);
        public static Int64 BytesFromMB(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.MB);
        public static Int64 BytesFromKB(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.KB);
        public static Int64 BytesFromBits(Double value) => (Int64)Math.Round(value / 8);
        public enum SizeUnit { B = 1, KB = 2, MB = 3, GB = 4, TB = 5 };
        //have to do this to make sure our constructor gets claled
        public static Int64 SmallestUnitFromUnitValue(Double unit_amt, SizeUnit unit) => _SmallestUnitFromUnitValue(unit_amt, unit);
        public static Int64 SizeFromHumanReadable(String sizeStrWithType) => _SizeFromHumanReadable(sizeStrWithType);
        public static Int64 SizeFromHumanReadable(String sizeStr, String sizeType) => _SizeFromHumanReadable(sizeStr, sizeType);
        public static Int64 SizeFromHumanReadable(String sizeStr, SizeUnit sizeType) => _SizeFromHumanReadable(sizeStr, sizeType);
        public static String HumanReadable(Double len, Int32 decimalPlaces = 2) => _HumanReadable(len, decimalPlaces);
    }
    public class BitSize : ByteBitSizeHelper<BitSize.SizeUnit>
    {
        static BitSize()
        {
            UnitIncrement = 1000;
            UnitDisplayChars = 4;
        }
        public static Int64 BitsFromTbits(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.tbit);
        public static Int64 BitsFromGbits(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.gbit);
        public static Int64 BitsFromMbits(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.mbit);
        public static Int64 BitsFromKbits(Double value) => SmallestUnitFromUnitValue(value, SizeUnit.bit);
        public static Int64 BitsFromBytes(Double value) => (Int64)Math.Round(value * 8);
        public static Int64 SmallestUnitFromUnitValue(Double unit_amt, SizeUnit unit) => _SmallestUnitFromUnitValue(unit_amt, unit);
        public static Int64 SizeFromHumanReadable(String sizeStrWithType) => _SizeFromHumanReadable(sizeStrWithType);
        public static Int64 SizeFromHumanReadable(String sizeStr, String sizeType) => _SizeFromHumanReadable(sizeStr, sizeType);
        public static Int64 SizeFromHumanReadable(String sizeStr, SizeUnit sizeType) => _SizeFromHumanReadable(sizeStr, sizeType);
        public static String HumanReadable(Double len, Int32 decimalPlaces = 2) => _HumanReadable(len, decimalPlaces);
        public enum SizeUnit { bit = 1, kbit = 2, mbit = 3, gbit = 4, tbit = 5 }
    }

    public class ByteBitSizeHelper<SizeUnit> where SizeUnit : struct, Enum
    {
        internal ByteBitSizeHelper()
        {
        }
        protected static Int32 UnitIncrement;
        protected static Int32 UnitDisplayChars;

        private static readonly Lazy<SizeUnit[]> sizesArr = new(() => (SizeUnit[])Enum.GetValues(typeof(SizeUnit)));
        protected static String _HumanReadable(Double len, Int32 decimalPlaces = 2)
        {

            var order = 0;
            while (len >= UnitIncrement && order < sizesArr.Value.Length - 1)
            {
                order++;
                len /= UnitIncrement;
            }
            return $"{len.ToString($"F{decimalPlaces}")} {sizesArr.Value[order].ToString().PadLeft(UnitDisplayChars)}";
        }
        private static Dictionary<SizeUnit, Int64> sizeUnitToBytesInit()
        {
            var size_multi = new Dictionary<SizeUnit, Int64>();
            var multi = 1;
            foreach (var size in sizesArr.Value)
            {
                size_multi[size] = multi;
                multi *= UnitIncrement;
            }
            return size_multi;
        }
        private static readonly Lazy<Dictionary<SizeUnit, Int64>> sizeUnitToBytes = new(sizeUnitToBytesInit);
        protected static Int64 _SizeFromHumanReadable(String sizeStr, SizeUnit sizeType) => String.IsNullOrEmpty(sizeStr) ? 0 : (Int64)Math.Round(Double.Parse(sizeStr) * sizeUnitToBytes.Value[sizeType]);
        protected static Int64 _SizeFromHumanReadable(String sizeStr, String sizeType)
        {
            return String.IsNullOrEmpty(sizeStr)
                ? 0
                : !Enum.TryParse<SizeUnit>(sizeType.Trim(), true, out var sizeUnit)
                ? throw new Exception($"Unable to parse size unit str: {sizeType}")
                : _SizeFromHumanReadable(sizeStr, sizeUnit);
        }
        private static readonly Lazy<Regex> regexSizeWithUnit = new(() => new Regex(@"(?<size>[0-9\.]+)\s*(?<unit>[a-zA-Z]+)", RegexOptions.Compiled));
        protected static Int64 _SizeFromHumanReadable(String sizeStrWithType)
        {
            if (String.IsNullOrEmpty(sizeStrWithType))
            {
                return 0;
            }

            var match = regexSizeWithUnit.Value.Match(sizeStrWithType);
            return !match.Success
                ? throw new Exception($"Seems invalid size str or cannot parse: {sizeStrWithType}")
                : _SizeFromHumanReadable(match.Groups["size"].Value, match.Groups["unit"].Value);
        }

        protected static Int64 _SmallestUnitFromUnitValue(Double unit_amt, SizeUnit unit) => (Int64)Math.Round(sizeUnitToBytes.Value[unit] * unit_amt);
    }
}

