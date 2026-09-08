using System;
using System.Globalization;
Console.WriteLine(string.Format(CultureInfo.GetCultureInfo(\"fr-FR\"), \"{0:N2} USD\", 12500000m));
Console.WriteLine(Convert.ToInt32(CultureInfo.GetCultureInfo(\"fr-FR\").NumberFormat.NumberGroupSizes[0]));
