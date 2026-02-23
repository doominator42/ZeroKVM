using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Net;
using System.Security.Authentication;
using ZeroKvm.ConfigFs;

namespace ZeroKvm;

internal static class ProgramOptions
{
    public static Option<HttpApp.ListenOption[]> Listen { get; } = new("--listen", "-l")
    {
        Required = true,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arg =>
        {
            HttpApp.ListenOption[] options = new HttpApp.ListenOption[arg.Tokens.Count];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ParseListenOption(arg, i);
            }

            return options;
        },
        HelpName = "address:port[,opt]",
        Description = "IP address and port to listen to. Options: tls,tls1.2,tls1.3",
    };

    public static Option<DirectoryInfo> ConfigFsGadgetPath { get; } = new("--gadget")
    {
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = _ => new DirectoryInfo(Path.Combine(UsbGadgetCfs.DefaultGadgetsBasePath, "zerokvm")),
        HelpName = "path",
        Description = "ConfigFS path for the USB gadget, including the gadget name",
    };

    public static Option<string> UdcName { get; } = new("--udc")
    {
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = _ =>
        {
            string[] udcs = Udc.GetUdcNames();
            if (udcs.Length == 0)
            {
                throw new Exception("No UDC found");
            }

            return udcs[0];
        },
        Description = "Name of the UDC to bind to",
    };

    public static Option<bool> Attach { get; } = new("--attach", "-a")
    {
        Arity = ArgumentArity.Zero,
        Description = "Bind to the UDC immediately",
    };

    public static Option<FileInfo> CertificatePath { get; } = new("--cert", "-c")
    {
        Arity = ArgumentArity.ExactlyOne,
        HelpName = "cert_path",
        Description = "File containing the TLS certificate, in DER or PEM format",
    };

    public static Option<FileInfo> CertificateKeyPath { get; } = new("--cert-key", "-k")
    {
        Arity = ArgumentArity.ExactlyOne,
        HelpName = "key_path",
        Description = "File containing the TLS private key, in DER or PEM format",
    };

    public static Option<bool> AutoCreateCertificate { get; } = new("--auto-create-cert")
    {
        Description = "Create the TLS certificate if inexistant",
    };

    public static Option<bool> HttpsRedirect { get; } = new("--https-redirect", "-r")
    {
        Description = "Redirect non-HTTPS requests to the configured HTTPS port",
    };

    public static Option<HttpApp.ProxyOption[]> Proxy = new("--proxy", "-p")
    {
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arg =>
        {
            HttpApp.ProxyOption[] options = new HttpApp.ProxyOption[arg.Tokens.Count];
            for (int i = 0; i < options.Length; i++)
            {
                var option = ParseProxyOption(arg, i);
                if (option is not null)
                {
                    options[i] = option;
                }
            }

            return options;
        },
        HelpName = "path:target_url",
        Description = "Proxy requests starting with path to the target_url",
    };

    public static Option<DirectoryInfo> WwwrootPath { get; } = new("--wwwroot")
    {
        Arity = ArgumentArity.ExactlyOne,
        HelpName = "path",
        Description = "Serve the assets from the specified path. Takes precedence over the embedded assets",
    };

    public static Option<bool> StatisticsEnabled { get; } = new("--statistics")
    {
        Description = "Print IO and processing statistics",
    };

    public static Option<bool> DebugEnabled { get; } = new("--debug")
    {
        Description = "Enable debug logging",
    };

    public static RootCommand CreateRootCommand()
    {
        return new()
        {
            TreatUnmatchedTokensAsErrors = true,
            Options =
            {
                Listen,
                ConfigFsGadgetPath,
                UdcName,
                Attach,
                CertificatePath,
                CertificateKeyPath,
                AutoCreateCertificate,
                HttpsRedirect,
                Proxy,
                WwwrootPath,
                StatisticsEnabled,
                DebugEnabled,
            },
        };
    }

    private static HttpApp.ListenOption ParseListenOption(ArgumentResult arg, int tokenIndex)
    {
        string[] args = arg.Tokens[tokenIndex].Value.Split(',');
        if (!IPEndPoint.TryParse(args[0], out IPEndPoint? endPoint))
        {
            arg.AddError($"Invalid IP endpoint: '{args[0]}'");
            endPoint = new(IPAddress.Any, 0);
        }

        SslProtocols sslProtocols = SslProtocols.None;
        for (int i = 1; i < args.Length; i++)
        {
            ReadOnlySpan<char> option = args[i];
            if (option.Equals("tls", StringComparison.OrdinalIgnoreCase))
            {
                sslProtocols |= SslProtocols.Tls12 | SslProtocols.Tls13;
            }
            else if (option.Equals("tls1.2", StringComparison.OrdinalIgnoreCase))
            {
                sslProtocols |= SslProtocols.Tls12;
            }
            else if (option.Equals("tls1.3", StringComparison.OrdinalIgnoreCase))
            {
                sslProtocols |= SslProtocols.Tls13;
            }
            else
            {
                arg.AddError($"Unknown listen option: '{arg}'");
            }
        }

        return new(endPoint, sslProtocols);
    }

    private static HttpApp.ProxyOption? ParseProxyOption(ArgumentResult arg, int tokenIndex)
    {
        string value = arg.Tokens[tokenIndex].Value;
        int separatorIndex = value.IndexOf(':');
        string path = separatorIndex <= 0 ? string.Empty : value.Substring(0, separatorIndex);
        Uri? destination;
        if (!path.StartsWith('/'))
        {
            arg.AddError("The source path must start with '/'");
            return null;
        }
        else if (!Uri.TryCreate(value.Substring(separatorIndex + 1), UriKind.Absolute, out destination) ||
            !(destination.Scheme == "http" || destination.Scheme == "https"))
        {
            arg.AddError($"Invalid destination URI: '{value.Substring(separatorIndex + 1)}'");
            return null;
        }
        else if (!string.IsNullOrEmpty(destination.Fragment))
        {
            arg.AddError("The proxy destination URI cannot contain a fragment");
            return null;
        }
        else if (!string.IsNullOrEmpty(destination.UserInfo))
        {
            arg.AddError("The proxy destination URI cannot contain a user/password");
            return null;
        }

        return new(path, destination);
    }
}
