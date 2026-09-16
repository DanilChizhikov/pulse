using System;
using System.Globalization;

namespace DTech.Pulse.Editor
{
	internal static class InitializationTimeFormat
	{
		private const double SecondsThresholdMilliseconds = 500d;
		private const double MillisecondsPerSecond = 1000d;
		private const int FractionalDigits = 2;
		private const string NumberFormat = "0.##";

		public static string Format(double milliseconds)
		{
			double roundedMilliseconds = Math.Round(milliseconds, FractionalDigits, MidpointRounding.AwayFromZero);
			if (Math.Abs(roundedMilliseconds) < SecondsThresholdMilliseconds)
			{
				return roundedMilliseconds.ToString(NumberFormat, CultureInfo.InvariantCulture) + " ms";
			}

			double seconds = milliseconds / MillisecondsPerSecond;
			return seconds.ToString(NumberFormat, CultureInfo.InvariantCulture) + " s";
		}
	}
}
