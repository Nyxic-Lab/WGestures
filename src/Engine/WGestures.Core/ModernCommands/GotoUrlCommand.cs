using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WGestures.Common.Annotation;

namespace WGestures.Core.Commands.Impl
{
    [Named("打开网址"), Serializable]
    public class GotoUrlCommand : AbstractCommand
    {
        private string _url;

        public string Url
        {
            get { return _url; }
            set
            {
                _url = value;
            }
        }

        public GotoUrlCommand()
        {
            Url = "";
        }

        public override void Execute()
        {
            if (Url != null)
            {
                if (!Url.Contains("://"))
                {
                    Url = "http://" + Url;
                }
            }
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }
    }
}
