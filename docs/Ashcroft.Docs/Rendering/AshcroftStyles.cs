namespace Ashcroft.Docs.Rendering;

/// <summary>
/// Raw CSS handed to MonorailCss via <c>ExtraStyles</c>. The page chrome is styled with utility
/// classes in the Razor components (<c>App.razor</c> / <c>MarkdownPage.razor</c>); this covers
/// what utilities can't reach — the page font, the rendered Markdown (<c>.prose</c>), and the
/// JS-injected copy buttons.
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
    html { scroll-behavior: smooth; scroll-padding-top: 90px; }
    body {
      margin: 0;
      font-family: "Geist", -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif;
      font-size: 17px; line-height: 1.65;
      text-rendering: optimizeLegibility;
    }
    code, pre, kbd, samp { font-family: "Geist Mono", ui-monospace, "SF Mono", Menlo, monospace; }
    ::selection { background: var(--acc-weak); }

    /* ===== Rendered Markdown ===== */
    .prose { counter-reset: sec; }
    .prose > :first-child { margin-top: 0; }
    .prose a { color: var(--acc); text-decoration: none; }
    .prose a:hover { color: var(--acc-ink); }

    /* Each h2 opens a numbered section: hairline rule, mono kicker, tight heading */
    .prose h2 {
      counter-increment: sec;
      margin: 0 0 12px; padding-top: 60px;
      border-top: 1px solid var(--line);
      font-size: 27px; font-weight: 600; letter-spacing: -0.02em; line-height: 1.2;
    }
    .prose h2::before {
      content: counter(sec, decimal-leading-zero);
      display: block; margin-bottom: 14px;
      font: 500 12px/1 "Geist Mono", ui-monospace, monospace;
      letter-spacing: 0.08em; color: var(--acc);
    }
    .prose h2 + p { color: var(--muted); }
    .prose h3 { font-size: 20px; font-weight: 600; letter-spacing: -0.01em; margin: 32px 0 8px; }

    .prose :not(pre) > code {
      font-size: 0.88em; font-weight: 500; color: var(--acc-ink);
      background: var(--panel); border: 1px solid var(--line);
      border-radius: 5px; padding: 1px 6px;
    }

    .prose pre { font-size: 12.5px; line-height: 1.75; }

    /* Echo the mock's code-bar: accent square before the language label, ghost copy button */
    .codeblock-lang { display: flex; align-items: center; gap: 8px; font-weight: 500; }
    .codeblock-lang::before {
      content: ""; width: 9px; height: 9px; flex: none;
      border-radius: 3px; background: oklch(0.86 0.05 257);
    }
    .codeblock-head .copy-btn { position: static; border: 0; background: transparent; }

    /* Live sample renders — bordered cards, like the result figures in the mock */
    .prose img {
      display: block; width: 100%; height: auto;
      margin: 18px 0 8px; border-radius: 12px;
      border: 1px solid var(--line-strong);
      box-shadow: 0 1px 0 rgba(20, 20, 40, 0.03);
    }

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

    .prose table { width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 15px; }
    .prose th, .prose td { text-align: left; padding: 8px 12px; border-bottom: 1px solid var(--line); }
    .prose th { color: var(--muted); font-weight: 600; }

    /* Alerts */
    .markdown-alert {
      margin: 20px 0; padding: 12px 16px;
      background: #fff; border: 1px solid var(--line);
      border-left: 3px solid var(--acc); border-radius: 10px;
    }
    .markdown-alert-important { border-left-color: #b45309; }
    .markdown-alert-warning, .markdown-alert-caution { border-left-color: #dc2626; }
    .markdown-alert-tip { border-left-color: #1f8a5b; }
    .markdown-alert p { margin: 6px 0; }

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
}
