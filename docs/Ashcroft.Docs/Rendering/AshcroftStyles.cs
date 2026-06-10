using System.Collections.Immutable;
using MonorailCss.Theme;

namespace Ashcroft.Docs.Rendering;

/// <summary>
/// Styling that lives outside the Razor utility classes. The rendered Markdown (<c>.prose</c>)
/// typography is expressed as MonorailCss prose rules in <see cref="ExtendProse"/>, layered over
/// Pennington's prose defaults. <see cref="Css"/> is the residue utilities and prose rules can't
/// reach — the design tokens, the page font, and the JS / code-bar chrome.
/// </summary>
public static class AshcroftStyles
{
    public const string Css = """
    :root {
      --font-sans: "Geist", -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif;
      --font-mono: "Geist Mono", ui-monospace, "SF Mono", Menlo, monospace;
      --acc: #2a6fdb;
      --acc-ink: #1f56ae;
      --acc-weak: oklch(0.955 0.028 257);
      --line: oklch(0.922 0.005 263);
      --line-strong: oklch(0.86 0.006 263);
      --panel: oklch(0.977 0.003 260);
      --muted: oklch(0.53 0.012 263);
      --faint: oklch(0.66 0.009 263);
    }
    body {
      margin: 0;
      font-family: "Geist", -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif;
      text-rendering: optimizeLegibility;
    }
    code, pre, kbd, samp { font-family: "Geist Mono", ui-monospace, "SF Mono", Menlo, monospace; }

    /* Echo the mock's code-bar: accent square before the language label, ghost copy button */
    .codeblock-lang { display: flex; align-items: center; gap: 8px; font-weight: 500; }
    .codeblock-lang::before {
      content: ""; width: 9px; height: 9px; flex: none;
      border-radius: 3px; background: oklch(0.86 0.05 257);
    }
    .codeblock-head .copy-btn { position: static; border: 0; background: transparent; }

    /* A code sample paired with the card it renders: stacked on small screens,
       side by side once there's room. The image cell is sticky so a tall sample
       keeps its output in view while you scroll the code. */
    .sample { margin: 18px 0 8px; }
    @media (min-width: 1024px) {
      .sample {
        display: grid;
        grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr);
        gap: 24px;
      }
      .sample > pre, .sample > .code-highlight-wrapper { margin: 0; }
      .sample > p { margin: 0; max-width: none; }
      .sample img { margin: 0; position: sticky; top: 90px; }
    }

    /* Two renders side by side (e.g. scrim on/off), each with a short mono caption. */
    .compare { display: grid; gap: 20px; margin: 18px 0 8px; }
    @media (min-width: 720px) { .compare { grid-template-columns: 1fr 1fr; } }
    .compare figure { margin: 0; }
    .compare img { margin: 0; }
    .compare figcaption {
      margin-top: 8px; text-align: center;
      font: 500 12px/1.4 "Geist Mono", ui-monospace, monospace;
      letter-spacing: 0.02em; color: var(--muted);
    }

    /* ===== JS-driven bits ===== */
    .copy-btn {
      position: absolute; top: 8px; right: 8px;
      border: 1px solid var(--line); border-radius: 6px;
      background: #fff; color: var(--faint);
      font: 500 12px "Geist", system-ui, sans-serif;
      padding: 4px 8px; cursor: pointer;
      transition: background 0.15s, color 0.15s;
    }
    .copy-btn:hover { background: var(--acc-weak); color: var(--acc-ink); }
    """;

    /// <summary>
    /// Rendered-Markdown typography, layered over Pennington's prose defaults. Each rule is appended
    /// after the existing <c>DEFAULT</c> rules so it wins by source order at equal (<c>:where</c>)
    /// specificity. Values reference the design tokens in <see cref="Css"/> directly.
    /// </summary>
    public static ProseCustomization ExtendProse(ProseCustomization existing) => new()
    {
        Customization = theme =>
        {
            var dict = existing?.Customization?.Invoke(theme)
                       ?? ImmutableDictionary<string, ProseElementRules>.Empty;

            var ours = ImmutableList.Create(
                Rule("a",                ("color", "var(--acc)"), ("text-decoration-line", "none")),
                Rule("a:hover",          ("color", "var(--acc-ink)")),

                // Each h2 opens a numbered section: hairline rule, mono kicker, tight heading.
                Rule("h2",               ("counter-increment", "sec"), ("margin", "0 0 12px"), ("padding-top", "60px"),
                                         ("border-top", "1px solid var(--line)"), ("font-size", "27px"),
                                         ("font-weight", "600"), ("letter-spacing", "-0.02em"), ("line-height", "1.2")),
                Rule("h2::before",       ("content", "counter(sec, decimal-leading-zero)"), ("display", "block"),
                                         ("margin-bottom", "14px"),
                                         ("font", "500 12px/1 \"Geist Mono\", ui-monospace, monospace"),
                                         ("letter-spacing", "0.08em"), ("color", "var(--acc)")),
                Rule("h2 + p",           ("color", "var(--muted)")),
                Rule("h3",               ("font-size", "20px"), ("font-weight", "600"),
                                         ("letter-spacing", "-0.01em"), ("margin", "32px 0 8px")),

                Rule(":not(pre) > code", ("font-size", "0.88em"), ("font-weight", "500"), ("color", "var(--acc-ink)"),
                                         ("background", "var(--panel)"), ("border", "1px solid var(--line)"),
                                         ("border-radius", "5px"), ("padding", "1px 6px")),
                Rule("pre",              ("font-size", "12.5px"), ("line-height", "1.75")),

                // Live sample renders — bordered cards, like the result figures in the mock.
                Rule("img",              ("display", "block"), ("width", "100%"), ("height", "auto"),
                                         ("margin", "18px 0 8px"), ("border-radius", "12px"),
                                         ("border", "1px solid var(--line-strong)"),
                                         ("box-shadow", "0 1px 0 rgba(20, 20, 40, 0.03)")),

                Rule("table",            ("width", "100%"), ("border-collapse", "collapse"),
                                         ("margin", "20px 0"), ("font-size", "15px")),
                Rule("th",               ("text-align", "left"), ("padding", "8px 12px"),
                                         ("border-bottom", "1px solid var(--line)"),
                                         ("color", "var(--muted)"), ("font-weight", "600")),
                Rule("td",               ("text-align", "left"), ("padding", "8px 12px"),
                                         ("border-bottom", "1px solid var(--line)")));

            var existingRules = dict.TryGetValue("DEFAULT", out var d) ? d.Rules : ImmutableList<ProseElementRule>.Empty;
            return dict.SetItem("DEFAULT", new ProseElementRules { Rules = existingRules.AddRange(ours) });
        },
    };

    // Pseudo-elements (::before) can't live inside :where(), so opt those rules out of the wrapper.
    private static ProseElementRule Rule(string selector, params (string Property, string Value)[] declarations) => new()
    {
        Selector = selector,
        UseWhereWrapper = !selector.Contains("::"),
        Declarations = declarations
            .Select(d => new ProseDeclaration { Property = d.Property, Value = d.Value })
            .ToImmutableList(),
    };
}
