using System;
using System.Globalization;
using System.Text;
using Cilica.Cli;

// Diagnostic text must not vary with the machine's language settings.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

return CilicaCommand.Run(args, Console.Out, Console.Error);
