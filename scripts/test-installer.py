"""Source contracts for installer choices; real .lnk checks run on Windows CI."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]



def section(name, source):
    match = re.search(rf"^\[{name}\]\n(.*?)(?=^\[|\Z)", source, re.M | re.S)
    assert match, f"Missing installer section: {name}"
    return match[1]


def entries(name, source):
    return [
        dict(re.findall(r'(\w+):\s*("[^"]*"|[^;\n]+)', line))
        for line in section(name, source).splitlines()
        if line and not line.startswith(";")
    ]


class InstallerContractTests(unittest.TestCase):
    edition = "Win-XinAi-De-Tools"
    app_id = "AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7"

    def setUp(self):
        self.source = (ROOT / f"installer/{self.edition}.iss").read_text(encoding="utf-8")
        # Normalize only the product name for the shared shortcut contracts.
        self.source = self.source.replace(self.edition, "Win-XinAi-De-Tools")

    def section(self, name):
        return section(name, self.source)

    def entries(self, name):
        return entries(name, self.source)

    def test_two_independent_checked_shortcut_tasks(self):
        tasks = self.entries("Tasks")
        self.assertEqual({task["Name"] for task in tasks}, {'"desktopicon"', '"startmenuicon"'})
        for task in tasks:
            self.assertNotIn("Flags", task)  # No unchecked/checkedonce defaults on upgrades.
            self.assertRegex(task["Description"], r'^"\{cm:\w+\}"$')
            self.assertEqual(task["GroupDescription"], '"{cm:ShortcutGroup}"')
        self.assertIn("UsePreviousTasks=no", self.section("Setup"))

    def test_every_shortcut_is_task_scoped_and_has_a_working_directory(self):
        icons = self.entries("Icons")
        self.assertEqual(len(icons), 3)
        app_icons = [icon for icon in icons if icon["Filename"] == '"{app}\\Win-XinAi-De-Tools.exe"']
        self.assertEqual(len(app_icons), 2)
        for icon in icons:
            self.assertIn(icon["Tasks"], {"startmenuicon", "desktopicon"})
            self.assertEqual(icon["WorkingDir"], '"{app}"')
            if "autodesktop" in icon["Name"]:
                self.assertEqual(icon["Tasks"], "desktopicon")
            else:
                self.assertTrue(icon["Name"].startswith('"{autoprograms}\\Win-XinAi-De-Tools\\'))
                self.assertEqual(icon["Tasks"], "startmenuicon")
        for icon in app_icons:
            self.assertEqual(icon["IconFilename"], '"{app}\\Win-XinAi-De-Tools.exe"')

    def test_installer_labels_are_localized(self):
        messages = dict(line.split("=", 1) for line in self.section("CustomMessages").splitlines() if "=" in line)
        for key in ["ShortcutGroup", "DesktopShortcut", "StartMenuShortcut", "LaunchApp"]:
            self.assertTrue(messages[f"english.{key}"])
            self.assertTrue(messages[f"chinesesimplified.{key}"])
        languages = self.section("Languages")
        self.assertIn('Name: "english"', languages)
        self.assertIn('Name: "chinesesimplified"', languages)

    def test_launch_is_optional_and_independent_of_shortcut_creation(self):
        runs = self.entries("Run")
        self.assertEqual(len(runs), 1)
        self.assertEqual(set(runs[0]["Flags"].split()), {"nowait", "postinstall", "skipifsilent"})
        self.assertNotIn("Tasks", runs[0])
        self.assertEqual(runs[0]["Description"], '"{cm:LaunchApp}"')

    def test_reinstall_cleanup_is_limited_to_exact_application_shortcuts(self):
        deletes = self.entries("InstallDelete")
        self.assertEqual({item["Name"] for item in deletes}, {
            '"{autoprograms}\\Win-XinAi-De-Tools.lnk"',
            '"{autoprograms}\\Win-XinAi-De-Tools\\Win-XinAi-De-Tools.lnk"',
            '"{autoprograms}\\Win-XinAi-De-Tools\\Uninstall Win-XinAi-De-Tools.lnk"',
            '"{autodesktop}\\Win-XinAi-De-Tools.lnk"',
        })
        for item in deletes:
            self.assertEqual(item["Type"], "files")
            self.assertNotIn("*", item["Name"])

    def test_installation_identity_and_elevation_remain_stable(self):
        setup = self.section("Setup")
        self.assertIn("AppId={{" + self.app_id + "}", setup)
        self.assertIn("PrivilegesRequired=admin", setup)
        self.assertIn("DefaultDirName={autopf}\\Win-XinAi-De-Tools", setup)


class MyGoInstallerContractTests(InstallerContractTests):
    edition = "Win-XinAi-De-Tools-MyGo"
    app_id = "ED82C612-79BA-4D1D-B2BD-5D6F9FA79E61"

    def test_identity_and_cleanup_are_independent(self):
        raw = (ROOT / f"installer/{self.edition}.iss").read_text(encoding="utf-8")
        for item in entries("InstallDelete", raw) + entries("Icons", raw):
            self.assertIn(self.edition, item["Name"])
        self.assertNotIn("AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7", raw)
        self.assertIn("DefaultDirName={autopf}\\" + self.edition, raw)


if __name__ == "__main__":
    unittest.main(verbosity=2)
