import { css } from 'lit';

// Colours and spacing come from custom properties, so a host can make the workbench look like the rest of its
// screens: the Umbraco backoffice passes its own. The defaults are plain and high-contrast.
export const shared = css`
  :host {
    --hg-text: var(--heliograph-text, #1b1b1b);
    --hg-muted: var(--heliograph-muted, #505a5f);
    --hg-border: var(--heliograph-border, #b1b4b6);
    --hg-surface: var(--heliograph-surface, #ffffff);
    --hg-subtle: var(--heliograph-subtle, #f3f2f1);
    --hg-accent: var(--heliograph-accent, #1d70b8);
    --hg-error: var(--heliograph-error, #d4351c);
    --hg-warning: var(--heliograph-warning, #b35c00);
    --hg-ok: var(--heliograph-ok, #00703c);
    font-family: var(--heliograph-font, system-ui, sans-serif);
    color: var(--hg-text);
  }
  button, .button {
    font: inherit;
    padding: 0.45rem 0.9rem;
    border: 1px solid var(--hg-accent);
    background: var(--hg-surface);
    color: var(--hg-accent);
    border-radius: 3px;
    cursor: pointer;
    text-decoration: none;
    display: inline-block;
  }
  button.primary { background: var(--hg-accent); color: #fff; }
  button:disabled { opacity: 0.5; cursor: not-allowed; }
  button:focus-visible, a:focus-visible, textarea:focus-visible, select:focus-visible, input:focus-visible {
    outline: 3px solid #ffdd00;
    outline-offset: 1px;
  }
  .actions { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; margin: 0.75rem 0; }
  .muted { color: var(--hg-muted); }
  .status { font-size: 0.9rem; }
  textarea {
    width: 100%;
    box-sizing: border-box;
    min-height: 18rem;
    font: 0.95rem/1.5 ui-monospace, SFMono-Regular, Menlo, monospace;
    padding: 0.75rem;
    border: 1px solid var(--hg-border);
  }
  label { font-weight: 600; display: block; margin-bottom: 0.25rem; }
  select, input[type='text'] { font: inherit; padding: 0.35rem; border: 1px solid var(--hg-border); }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; padding: 0.4rem 0.6rem; border-bottom: 1px solid var(--hg-border); vertical-align: top; }
  code { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
`;

/** Saves a file the server sent, under a name, the way a download link would. */
export function saveFile(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const link = Object.assign(document.createElement('a'), { href: url, download: name });
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
