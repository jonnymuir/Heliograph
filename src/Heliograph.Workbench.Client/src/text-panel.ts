import { LitElement, css, html } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import type { DictionaryEntry, Draft, WorkbenchSource } from './source.js';
import { shared } from './styles.js';

/** The email or the text message, written here as plain text, with the dictionary's tags a click away. */
@customElement('heliograph-text-panel')
export class TextPanel extends LitElement {
  @property({ attribute: false }) source!: WorkbenchSource;
  @property({ attribute: false }) draft!: Draft;
  @property({ attribute: false }) tags: DictionaryEntry[] = [];
  @property() kind: 'email' | 'sms' = 'email';
  @state() private text = '';
  @state() private status = '';
  @query('textarea') private area!: HTMLTextAreaElement;

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('draft') || changed.has('kind')) {
      this.text = (this.kind === 'email' ? this.draft.email : this.draft.sms) ?? (this.kind === 'email' ? 'Subject: \n\n' : '');
    }
  }

  override render() {
    const changed = this.text !== ((this.kind === 'email' ? this.draft.email : this.draft.sms) ?? '');
    return html`
      <div class="layout">
        <div>
          <label for="text">${this.kind === 'email' ? 'Email: a first line “Subject: …”, a blank line, then the body' : 'Text message'}</label>
          <textarea id="text" .value=${this.text} @input=${(e: Event) => (this.text = (e.target as HTMLTextAreaElement).value)}></textarea>
          <div class="actions">
            <button class="primary" ?disabled=${!changed} @click=${this.save}>Save</button>
            <span class="status muted" role="status">${this.status}</span>
          </div>
          ${this.kind === 'email'
            ? html`<p class="muted">You can use <code># Heading</code>, <code>## Smaller heading</code>, <code>- list items</code>,
                <code>**bold**</code>, <code>[a link](https://…)</code> and <code>---</code> for a rule.</p>`
            : html`<p class="muted">Plain text. The preview shows how many messages it will be charged as.</p>`}
        </div>
        <aside aria-label="Tags to insert">
          <h3>Insert a tag</h3>
          <ul>
            ${this.tags.filter((t) => t.tag).map((t) => html`<li><button class="tag" @click=${() => this.insert(t)} title=${t.type}>${t.tag}</button></li>`)}
          </ul>
        </aside>
      </div>`;
  }

  private insert(entry: DictionaryEntry) {
    const area = this.area;
    const insert = entry.type === 'yes or no' ? `${entry.tag}\n\n((end if))` : entry.tag!;
    const { selectionStart: start, selectionEnd: end } = area;
    this.text = this.text.slice(0, start) + insert + this.text.slice(end);
    void this.updateComplete.then(() => {
      area.focus();
      area.setSelectionRange(start + insert.length, start + insert.length);
    });
  }

  private save = async () => {
    this.status = 'Saving…';
    const outcome = await this.source.save(this.draft.key, { expectedRevision: this.draft.revision, [this.kind]: this.text });
    this.status = outcome.saved ? 'Saved.' : outcome.conflict ? 'Someone else has saved this since you opened it. Copy your text, reload and try again.' : 'Not saved.';
    this.dispatchEvent(new CustomEvent('saved', { detail: outcome, bubbles: true, composed: true }));
  };

  static override styles = [
    shared,
    css`
      .layout { display: grid; grid-template-columns: minmax(0, 1fr) 15rem; gap: 1.25rem; }
      @media (max-width: 50rem) { .layout { grid-template-columns: 1fr; } }
      aside ul { list-style: none; padding: 0; margin: 0; max-height: 24rem; overflow: auto; }
      aside li { margin-bottom: 0.3rem; }
      .tag { font-family: ui-monospace, Menlo, monospace; font-size: 0.85rem; padding: 0.25rem 0.5rem; width: 100%; text-align: left; }
      h3 { margin: 0 0 0.5rem; font-size: 1rem; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-text-panel': TextPanel;
  }
}
