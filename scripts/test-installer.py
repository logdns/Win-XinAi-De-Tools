"""Source contracts for installer choices; real .lnk checks run on Windows CI."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "installer/Win-XinAi-De-Tools.iss").read_text(encoding="utf-8")


def section(name):
    match = re.search(rf"^\[{name}\]\n(.*?)(?=^\[|\Z)", SOURCE, re.M | re.S)
    assert match, f"Missing installer section: {name}"
    return match[1]


def entries(name):
    return [
        dict(re.findall(r'(\w+):\s*("[^"]*"|[^;\n]+)', line))
        for line in section(name).splitlines()
        if line and not line.startswith(";")
    ]


class InstallerContractTests(unittest.TestCase):
    def test_two_independent_checked_shortcut_tasks(self):
        tasks = entries("Tasks")
        self.assertEqual({task["Name"] for task in tasks}, {'"desktopicon"', '"startmenuicon"'})
        for task in tasks:
            self.assertNotIn("Flags", task)  # No unchecked/checkedonce defaults on upgrades.
            self.assertRegex(task["Description"], r'^"\{cm:\w+\}"$')
            self.assertEqual(task["GroupDescription"], '"{cm:ShortcutGroup}"')
        self.assertIn("UsePreviousTasks=no", section("Setup"))

    def test_every_shortcut_is_task_scoped_and_has_a_working_directory(self):
        icons = entries("Icons")
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
        messages = dict(line.split("=", 1) for line in section("CustomMessages").splitlines() if "=" in line)
        for key in ["ShortcutGroup", "DesktopShortcut", "StartMenuShortcut", "LaunchApp"]:
            self.assertTrue(messages[f"english.{key}"])
            self.assertTrue(messages[f"chinesesimplified.{key}"])
        languages = section("Languages")
        self.assertIn('Name: "english"', languages)
        self.assertIn('Name: "chinesesimplified"', languages)

    def test_launch_is_optional_and_independent_of_shortcut_creation(self):
        runs = entries("Run")
        self.assertEqual(len(runs), 1)
        self.assertEqual(set(runs[0]["Flags"].split()), {"nowait", "postinstall", "skipifsilent"})
        self.assertNotIn("Tasks", runs[0])
        self.assertEqual(runs[0]["Description"], '"{cm:LaunchApp}"')

    def test_reinstall_cleanup_is_limited_to_exact_application_shortcuts(self):
        deletes = entries("InstallDelete")
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
        setup = section("Setup")
        self.assertIn("AppId={{AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7}", setup)
        self.assertIn("PrivilegesRequired=admin", setup)
        self.assertIn("DefaultDirName={autopf}\\Win-XinAi-De-Tools", setup)


if __name__ == "__main__":
    unittest.main(verbosity=2)
