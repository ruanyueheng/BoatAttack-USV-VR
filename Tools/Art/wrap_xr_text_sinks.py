"""One-off: wrap `<target>=EXPR;` text assignments in FishingXRText.Adapt(EXPR). Usage: wrap_xr_text_sinks.py <file> <target>..."""
import re, sys

def wrap(path, targets):
    s = open(path, encoding='utf-8').read()
    pat = re.compile('|'.join(re.escape(t) + r'\s*=(?!=)' for t in targets))
    out, i, n = [], 0, 0
    while True:
        m = pat.search(s, i)
        if not m:
            out.append(s[i:]); break
        j = m.end(); depth = 0; quoted = False; k = j
        while k < len(s):
            ch = s[k]
            if quoted:
                if ch == '\\': k += 2; continue
                if ch == '"': quoted = False
            elif ch == '"': quoted = True
            elif ch in '([{': depth += 1
            elif ch in ')]}': depth -= 1
            elif ch == ';' and depth == 0: break
            k += 1
        expr = s[j:k]
        if 'FishingXRText.Adapt' in expr:
            out.append(s[i:k]); i = k; continue
        out.append(s[i:j] + 'FishingXRText.Adapt(' + expr.strip() + ')'); i = k; n += 1
    open(path, 'w', encoding='utf-8').write(''.join(out))
    print(path, n)

if __name__ == '__main__':
    wrap(sys.argv[1], sys.argv[2:])
