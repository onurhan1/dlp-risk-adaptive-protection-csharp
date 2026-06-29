import re, glob, os

skip_words = {'React','Set','Date','HTMLElement','SearchableMultiSelect',
              'Sparkles','SlidersHorizontal','Plus','Minus','ClipboardList',
              'Search','Boolean','Array','Incident','Shield','Suspense',
              'LoadingOverlay','HeatmapSection','IncidentTable',
              'ExceptionRecommendation','Calendar','BookOpen','FormData',
              'JSON','Math','Object','Promise','Error','Response','Request',
              'Header','Footer','Body','Content','Provider','Context',
              'Tooltip','Modal','Button','Input','Select','Option','Label',
              'Form','Table','Row','Column','Cell','Icon','Image','Link',
              'Fragment','Component','Element','Node','Ref','Memo','Callback'}

files = glob.glob('dashboard/**/*.tsx', recursive=True)
results = []

for f in files:
    if 'node_modules' in f or '.test.' in f or 'buildHooks' in f:
        continue
    lines = open(f, encoding='utf-8').readlines()
    basename = os.path.basename(f)
    for i, line in enumerate(lines, 1):
        if '{t(' in line:
            continue
        # Match >English Text<  or  label="English Text"  or  placeholder="English Text"
        for pat in [r'>([A-Z][a-zA-Z /:]{2,})</', r'label="([A-Z][a-zA-Z /]{2,})"', r"placeholder=\"([A-Z][a-zA-Z /,'.]{2,})\"", r"'([A-Z][a-z]+ [A-Z][a-z]+)'"]:
            for m in re.findall(pat, line):
                m = m.strip()
                if m not in skip_words and len(m) > 3:
                    results.append(f'{basename}:{i}: "{m}"')

for r in sorted(set(results)):
    print(r)
