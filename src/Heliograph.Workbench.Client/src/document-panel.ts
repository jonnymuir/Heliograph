import { LitElement, css, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { Draft, Problem, WorkbenchSource } from './source.js';
import { saveFile, shared } from './styles.js';

/**
 * The document part. Heliograph builds no word processor, so this is about moving the file between the author's
 * word processor and Heliograph as simply as possible: a starter to begin from, drop to upload, and the file back
 * with a comment at each problem.
 */
@customElement('heliograph-document-panel')
export class DocumentPanel extends LitElement {
  @property({ attribute: false }) source!: WorkbenchSource;
  @property({ attribute: false }) draft!: Draft;
  @property({ attribute: false }) problems: Problem[] = [];
  @state() private dragging = false;
  @state() private busy = '';

  override render() {
    const documentProblems = this.problems.some((p) => !p.location || !['email', 'email subject', 'sms'].includes(p.location.part));
    return html`
      <p>
        Write the letter in Word, Word in Microsoft 365, LibreOffice or any word processor that saves a Word document
        (.docx). Copy tags from the <strong>Tags</strong> tab.
      </p>
      <div class="actions">
        <button @click=${() => this.download('starter', `${this.draft.key}-starter.docx`)}>Download a starter document</button>
        ${this.draft.hasDocument
          ? html`<button class="primary" @click=${() => this.open('word')}>Open in Word</button>
              <button class="primary" @click=${() => this.open('libreOffice')}>Open in LibreOffice</button>
              <button @click=${() => this.download('document', `${this.draft.key}.docx`)}>Download the document</button>
              ${documentProblems
                ? html`<button @click=${() => this.download('annotated', `${this.draft.key}-with-comments.docx`)}>Download with comments</button>`
                : null}
              <button @click=${this.removeDocument}>Remove the document</button>`
          : null}
      </div>
      <label
        class="drop ${this.dragging ? 'over' : ''}"
        @dragover=${(e: DragEvent) => { e.preventDefault(); this.dragging = true; }}
        @dragleave=${() => (this.dragging = false)}
        @drop=${this.dropped}>
        <span>${this.draft.hasDocument ? 'Drop a new version here' : 'Drop the document here'}, or choose a file</span>
        <input type="file" accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document" @change=${this.chosen} />
      </label>
      <p class="status muted" role="status">${this.busy}</p>
      ${this.draft.hasDocument
        ? html`<p class="muted">
            <strong>Open in Word</strong> or <strong>LibreOffice</strong> edits the document where it is: save it in your
            word processor and it is saved here. Come back to this page to see what is wrong with it. The link works for
            eight hours.
          </p>`
        : null}
      ${documentProblems
        ? html`<p class="muted">
            <strong>Download with comments</strong> gives you your own document back with a comment at each problem. Fix
            them and upload it again; Heliograph's comments come out by themselves.
          </p>`
        : null}`;
  }

  /** Hands the document to the word processor through an address it opens and saves back to. */
  private async open(editor: 'word' | 'libreOffice') {
    const link = await this.source.editLink(this.draft.key);
    if (!link) {
      this.busy = 'The document could not be opened.';
      return;
    }
    this.busy = `Opening in ${editor === 'word' ? 'Word' : 'LibreOffice'}… Save there when you are done, then come back here.`;
    window.location.href = link[editor];
  }

  private async download(which: 'starter' | 'document' | 'annotated', name: string) {
    const blob = await this.source.file(this.draft.key, which);
    if (blob) saveFile(blob, name);
  }

  private dropped = (e: DragEvent) => {
    e.preventDefault();
    this.dragging = false;
    const file = e.dataTransfer?.files[0];
    if (file) void this.upload(file);
  };

  private chosen = (e: Event) => {
    const input = e.target as HTMLInputElement;
    if (input.files?.[0]) void this.upload(input.files[0]);
    input.value = '';
  };

  private async upload(file: File) {
    this.busy = `Checking ${file.name}…`;
    const outcome = await this.source.upload(this.draft.key, this.draft.revision, file);
    this.busy = outcome.saved ? `${file.name} uploaded.` : outcome.conflict ? 'Someone else has saved this since you opened it. Reload and try again.' : 'Not uploaded.';
    this.dispatchEvent(new CustomEvent('saved', { detail: outcome, bubbles: true, composed: true }));
  }

  private removeDocument = async () => {
    const outcome = await this.source.save(this.draft.key, { expectedRevision: this.draft.revision, remove: ['document'] });
    this.dispatchEvent(new CustomEvent('saved', { detail: outcome, bubbles: true, composed: true }));
  };

  static override styles = [
    shared,
    css`
      .drop {
        display: block;
        border: 2px dashed var(--hg-border);
        padding: 2rem;
        text-align: center;
        font-weight: 400;
        cursor: pointer;
        background: var(--hg-subtle);
      }
      .drop.over { border-color: var(--hg-accent); background: var(--hg-surface); }
      .drop input { display: block; margin: 0.75rem auto 0; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-document-panel': DocumentPanel;
  }
}
