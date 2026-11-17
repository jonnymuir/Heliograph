import { LitElement, css, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { Draft, Parts, WorkbenchSource } from './source.js';
import { saveFile, shared } from './styles.js';

/** Every part as one made-up recipient would get it. */
@customElement('heliograph-preview-panel')
export class PreviewPanel extends LitElement {
  @property({ attribute: false }) source!: WorkbenchSource;
  @property({ attribute: false }) draft!: Draft;
  @property({ attribute: false }) samples: string[] = [];
  @state() private sample = '';
  @state() private parts: Parts | null = null;
  @state() private pdf: string | null = null;
  @state() private status = '';

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('samples') && !this.samples.includes(this.sample)) this.sample = this.samples[0] ?? '';
  }

  override render() {
    if (this.samples.length === 0) {
      return html`<p>This dictionary has no sample recipients, so there is nothing to preview with.</p>`;
    }
    return html`
      <div class="actions">
        <label for="sample">Sample recipient</label>
        <select id="sample" @change=${(e: Event) => (this.sample = (e.target as HTMLSelectElement).value)}>
          ${this.samples.map((s) => html`<option ?selected=${s === this.sample}>${s}</option>`)}
        </select>
        <button class="primary" @click=${this.run}>Preview</button>
        <span class="status muted" role="status">${this.status}</span>
      </div>
      ${this.parts ? this.show(this.parts) : null}`;
  }

  private show(parts: Parts) {
    return html`
      ${parts.email
        ? html`<section>
            <h3>Email</h3>
            <p><span class="muted">Subject:</span> <strong>${parts.email.subject}</strong></p>
            <iframe title="The email as the recipient sees it" sandbox="" .srcdoc=${parts.email.html}></iframe>
          </section>`
        : null}
      ${parts.sms
        ? html`<section>
            <h3>Text message</h3>
            <p class="bubble">${parts.sms.text}</p>
            <p class="muted">${parts.sms.segments} ${parts.sms.segments === 1 ? 'message' : 'messages'}, ${parts.sms.encoding} encoding</p>
          </section>`
        : null}
      ${parts.hasDocument
        ? html`<section>
            <h3>Letter</h3>
            <div class="actions">
              <button @click=${this.downloadLetter}>Download the letter (.docx)</button>
            </div>
            ${this.pdf ? html`<iframe class="pdf" title="The letter as a PDF" src=${this.pdf}></iframe>` : html`<p class="muted">This site has no route to PDF, so the letter is shown as a download only.</p>`}
          </section>`
        : null}`;
  }

  private run = async () => {
    this.status = 'Rendering…';
    this.parts = await this.source.preview(this.draft.key, this.sample);
    if (this.pdf) URL.revokeObjectURL(this.pdf);
    const pdf = this.parts.hasDocument ? await this.source.file(this.draft.key, 'preview-pdf', this.sample) : null;
    this.pdf = pdf ? URL.createObjectURL(pdf) : null;
    this.status = this.parts.diagnostics.length > 0 ? 'Not rendered: see the problems above.' : '';
    this.dispatchEvent(new CustomEvent('problems', { detail: this.parts.diagnostics, bubbles: true, composed: true }));
  };

  private downloadLetter = async () => {
    const blob = await this.source.file(this.draft.key, 'preview-document', this.sample);
    if (blob) saveFile(blob, `${this.draft.key}-${this.sample}.docx`);
  };

  static override styles = [
    shared,
    css`
      section { margin-top: 1.5rem; }
      h3 { font-size: 1.05rem; margin: 0 0 0.5rem; }
      iframe { width: 100%; height: 28rem; border: 1px solid var(--hg-border); background: #fff; }
      iframe.pdf { height: 40rem; }
      .bubble { white-space: pre-wrap; background: var(--hg-subtle); border-radius: 1rem; padding: 0.75rem 1rem; max-width: 26rem; }
      label { display: inline; margin: 0; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-preview-panel': PreviewPanel;
  }
}
