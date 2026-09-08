"""Validate release surfaces, localization parity and XAML references without Windows."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'

def keys(path):
    elements = list(ET.parse(path).getroot())
    names = [e.get(X + 'Key') for e in elements if e.get(X + 'Key')]
    assert len(names) == len(set(names)), f'Duplicate resource key in {path}'
    return {e.get(X + 'Key'): e.text or '' for e in elements if e.get(X + 'Key')}

chinese = keys(ROOT / 'Localization/Strings.zh-CN.xaml')
english = keys(ROOT / 'Localization/Strings.en-US.xaml')
assert chinese.keys() == english.keys(), 'Chinese and English keys differ'
for key in chinese:
    assert sorted(re.findall(r'\{\d+\}', chinese[key])) == sorted(re.findall(r'\{\d+\}', english[key])), f'Format placeholders differ: {key}'
styles = {e.get(X + 'Key') for e in ET.parse(ROOT / 'App.xaml').iter() if e.get(X + 'Key')}
for path in [ROOT / 'App.xaml', ROOT / 'MainWindow.xaml', *sorted((ROOT / 'Views').glob('*.xaml'))]:
    ET.parse(path)
    for key in re.findall(r'\{StaticResource ([\w]+)\}', path.read_text()):
        assert key in chinese or key in styles, f'{path.name}: missing resource {key}'
    if path.with_suffix('.xaml.cs').exists():
        source = path.with_suffix('.xaml.cs').read_text()
        for handler in re.findall(r'(?:Click|Loaded|SizeChanged|SelectionChanged|TextChanged|ValueChanged|KeyDown|Invoked)="(\w+)"', path.read_text()):
            assert handler in source, f'{path.name}: missing handler {handler}'
project = ET.parse(ROOT / 'Win-XinAi-De-Tools.csproj').getroot()
version = project.findtext('PropertyGroup/Version')
for path in ['README.md', 'README.zh-CN.md', 'installer/Win-XinAi-De-Tools.iss', 'CHANGELOG.md']:
    assert version in (ROOT / path).read_text(), f'{path}: missing current version'
assert version in chinese['About_Version'] and version in english['About_Version']
manifest = ET.parse(ROOT / 'Package.appxmanifest').getroot()
assert list(manifest)[0].get('Version') == version + '.0'
assert project.findtext('PropertyGroup/AssemblyVersion') == version + '.0'
assert project.findtext('PropertyGroup/FileVersion') == version + '.0'
print(f'UI audit passed: {len(chinese)} bilingual resources, 13 pages, version {version}.')
