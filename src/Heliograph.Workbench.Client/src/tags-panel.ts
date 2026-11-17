import { LitElement, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { DictionaryEntry } from './source.js';
import { shared } from './styles.js';

/** Every field in the dictionary with its tag, ready to copy into the document, so nobody types one from memory. */
@customElement('heliograph-tags-panel')
export class TagsPanel extends LitElement {
  @property({ attribute: false }) tags: DictionaryEntry[] = [];
  @property() dictionary = '';
  @state() private copied = '';

  override render() {
    return html`
      <p>The fields in the dictionary <strong>${this.dictionary}</strong>. Copy a tag exactly as shown, brackets included.</p>
      <table>
        <thead><tr><th scope="col">Tag</th><th scope="col">Field</th><th scope="col">Type</th><th scope="col"><span class="muted">Copy</span></th></tr></thead>
        <tbody>
          ${this.tags.map(
            (t) => html`<tr>
              <td>${t.tag ? html`<code>${t.tag}</code>` : html`<span class="muted">a list: use its fields in a table row</span>`}</td>
              <td>${t.name}${t.list ? html`<br /><span class="muted">in ${t.list}</span>` : null}</td>
              <td>${t.type}</td>
              <td>${t.tag ? html`<button @click=${() => this.copy(t.tag!)} aria-label="Copy ${t.tag}">${this.copied === t.tag ? 'Copied' : 'Copy'}</button>` : null}</td>
            </tr>`,
          )}
        </tbody>
      </table>
      <p class="muted">A yes or no field shows or hides what is between its tag and <code>((end if))</code>.</p>`;
  }

  private async copy(tag: string) {
    await navigator.clipboard.writeText(tag);
    this.copied = tag;
  }

  static override styles = [shared];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-tags-panel': TagsPanel;
  }
}
