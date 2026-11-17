import { LitElement, css, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import './document-panel.js';
import './preview-panel.js';
import './problems-list.js';
import './tags-panel.js';
import './text-panel.js';
import type { DictionaryEntry, Draft, Problem, SaveOutcome, WorkbenchSource } from './source.js';
import { shared } from './styles.js';

type Tab = 'document' | 'email' | 'sms' | 'preview' | 'tags';
const tabs: Array<[Tab, string]> = [['document', 'Letter'], ['email', 'Email'], ['sms', 'Text message'], ['preview', 'Preview'], ['tags', 'Tags']];

/** One correspondence: its problems, a tab for each part, a preview, the tags, and publishing. */
@customElement('heliograph-correspondence')
export class CorrespondenceView extends LitElement {
  @property({ attribute: false }) source!: WorkbenchSource;
  @property() key = '';
  @state() private draft: Draft | null = null;
  @state() private problems: Problem[] = [];
  @state() private tags: DictionaryEntry[] = [];
  @state() private samples: string[] = [];
  @state() private tab: Tab = 'document';
  @state() private status = '';

  override async willUpdate(changed: Map<string, unknown>) {
    if (changed.has('key') && this.key) await this.load(true);
  }

  private async load(dictionaryToo: boolean) {
    this.draft = await this.source.draft(this.key);
    if (!this.draft) return;
    this.problems = await this.source.check(this.key);
    if (dictionaryToo) {
      [this.tags, this.samples] = await Promise.all([
        this.source.dictionary(this.draft.dictionary).catch(() => []),
        this.source.samples(this.draft.dictionary).catch(() => []),
      ]);
    }
  }

  override render() {
    const draft = this.draft;
    if (!draft) return html`<p>Loading…</p>`;
    const blocked = this.problems.some((p) => p.severity === 'Error');
    return html`
      <header>
        <div>
          <h2>${draft.name}</h2>
          <p class="muted">
            <code>${draft.key}</code> · dictionary <strong>${draft.dictionary}</strong> ·
            ${draft.latestVersion ? html`version ${draft.latestVersion} is what recipients get` : 'not published yet'}
          </p>
        </div>
        <div class="actions">
          <button class="primary" ?disabled=${blocked} @click=${this.publish}>Publish</button>
          <span class="status muted" role="status">${this.status}</span>
        </div>
      </header>
      <heliograph-problems .problems=${this.problems}></heliograph-problems>
      <nav role="tablist" aria-label="Parts">
        ${tabs.map(([id, label]) => html`<button role="tab" aria-selected=${this.tab === id} class=${this.tab === id ? 'on' : ''} @click=${() => (this.tab = id)}>${label}</button>`)}
      </nav>
      <div role="tabpanel" @saved=${this.saved} @problems=${(e: CustomEvent<Problem[]>) => e.detail.length && (this.problems = e.detail)}>
        ${this.panel(draft)}
      </div>`;
  }

  private panel(draft: Draft) {
    switch (this.tab) {
      case 'document':
        return html`<heliograph-document-panel .source=${this.source} .draft=${draft} .problems=${this.problems}></heliograph-document-panel>`;
      case 'email':
      case 'sms':
        return html`<heliograph-text-panel .source=${this.source} .draft=${draft} .tags=${this.tags} .kind=${this.tab}></heliograph-text-panel>`;
      case 'preview':
        return html`<heliograph-preview-panel .source=${this.source} .draft=${draft} .samples=${this.samples}></heliograph-preview-panel>`;
      default:
        return html`<heliograph-tags-panel .tags=${this.tags} .dictionary=${draft.dictionary}></heliograph-tags-panel>`;
    }
  }

  private saved = async (e: CustomEvent<SaveOutcome>) => {
    if (e.detail.saved) await this.load(false);
  };

  private publish = async () => {
    const outcome = await this.source.publish(this.key);
    this.status = outcome.version ? `Published as version ${outcome.version}.` : 'Not published: see the problems.';
    await this.load(false);
    this.dispatchEvent(new CustomEvent('published', { bubbles: true, composed: true }));
  };

  static override styles = [
    shared,
    css`
      header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
      h2 { margin: 0; font-size: 1.5rem; }
      nav { display: flex; gap: 0.25rem; border-bottom: 1px solid var(--hg-border); margin: 1.5rem 0 1rem; }
      nav button { border: none; border-bottom: 4px solid transparent; border-radius: 0; color: var(--hg-text); }
      nav button.on { border-bottom-color: var(--hg-accent); font-weight: 700; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-correspondence': CorrespondenceView;
  }
}
