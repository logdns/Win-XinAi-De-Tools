// resources embeds the existing app icon and an elevated, DPI-aware manifest.
package main

import (
	"flag"
	"log"
	"os"

	"github.com/tc-hib/winres"
	"github.com/tc-hib/winres/version"
)

func main() {
	arch := flag.String("arch", "amd64", "amd64 or arm64")
	flag.Parse()
	if *arch != "amd64" && *arch != "arm64" {
		log.Fatal("unsupported MyGo architecture")
	}
	iconFile, err := os.Open("../Assets/Win-XinAi-De-Tools.ico")
	if err != nil {
		log.Fatal(err)
	}
	defer iconFile.Close()
	icon, err := winres.LoadICO(iconFile)
	if err != nil {
		log.Fatal(err)
	}
	set := winres.ResourceSet{}
	if err := set.SetIcon(winres.ID(1), icon); err != nil {
		log.Fatal(err)
	}
	set.SetManifest(winres.AppManifest{ExecutionLevel: winres.RequireAdministrator, Compatibility: winres.Win10AndAbove, DPIAwareness: winres.DPIPerMonitorV2, LongPathAware: true, UseCommonControlsV6: true})
	info := version.Info{}
	info.SetFileVersion("1.12.1.0")
	info.SetProductVersion("1.12.1.0")
	for key, value := range map[string]string{"ProductName": "Win-XinAi-De-Tools-MyGo", "MyGoIdentifier": "com.logdns.winxinai.mygo", "FileDescription": "Win-XinAi-De-Tools-MyGo · Built with MyGo", "FileVersion": "1.12.1", "ProductVersion": "1.12.1", "OriginalFilename": "Win-XinAi-De-Tools-MyGo.exe", "CompanyName": "Fengge Network"} {
		if err := info.Set(0x409, key, value); err != nil {
			log.Fatal(err)
		}
	}
	set.SetVersionInfo(info)
	output, err := os.Create("rsrc_windows_" + *arch + ".syso")
	if err != nil {
		log.Fatal(err)
	}
	if err := set.WriteObject(output, winres.Arch(*arch)); err != nil {
		log.Fatal(err)
	}
	if err := output.Close(); err != nil {
		log.Fatal(err)
	}
}
