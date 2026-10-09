using System;
using System.IO;
using System.Reflection;
using System.Threading;
using T50LabelPrinter;
[assembly: AssemblyVersion("1.6.3.0")]
static class UpdateIntegration
{
    static int Main(string[] args)
    {
        try
        {
            string server=args[0], directory=Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
            using(var client=new EasyUpdateClient())
            {
                var release=client.CheckAsync(server,EasyUpdateSettings.DefaultPackage,CancellationToken.None).GetAwaiter().GetResult();
                if(!release.UpdateAvailable || release.VersionCode!=10700) throw new Exception("Upgrade from 1.6.3 not detected");
                string zip=Path.Combine(directory,"downloaded-v1.7.0.zip");
                client.DownloadAsync(release,zip,null,CancellationToken.None).GetAwaiter().GetResult();
                if(new FileInfo(zip).Length!=release.Size) throw new Exception("Download size mismatch");
                string sentinel=Path.Combine(directory,"existing.zip"); File.WriteAllText(sentinel,"keep existing file");
                string hash=release.Sha256; release.Sha256=new string('0',64);
                bool rejected=false;
                try { client.DownloadAsync(release,sentinel,null,CancellationToken.None).GetAwaiter().GetResult(); } catch(InvalidDataException) { rejected=true; }
                if(!rejected || File.ReadAllText(sentinel)!="keep existing file") throw new Exception("Hash failure replaced existing file");
                release.Sha256=hash;
                string version=release.VersionName; release.VersionName="wrong-version"; rejected=false;
                try { client.DownloadAsync(release,sentinel,null,CancellationToken.None).GetAwaiter().GetResult(); } catch(InvalidDataException) { rejected=true; }
                if(!rejected || File.ReadAllText(sentinel)!="keep existing file") throw new Exception("Manifest mismatch accepted");
                release.VersionName=version;
                var cancelled=new CancellationTokenSource(); cancelled.Cancel(); rejected=false;
                try { client.DownloadAsync(release,sentinel,null,cancelled.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { rejected=true; }
                if(!rejected || Directory.GetFiles(directory,"*.part").Length!=0) throw new Exception("Cancellation or cleanup failed");
                Console.WriteLine("PASS real EasyUpdate service: old client detects update, ZIP download verified, SHA256/manifest failures preserve existing file, cancellation cleans partial files.");
            }
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
