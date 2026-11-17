import { LitElement, css, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import './correspondence-view.js';
import { HttpWorkbenchSource, type Summary, type WorkbenchSource } from './source.js';
import { shared } from './styles.js';

/**
 * The author's screen: every correspondence, and the one being worked on. Give it `api` (the address of
 * Heliograph.Api) or set `source` to calls of the host's own.
 */
@customElement('heliograph-workbench')
export class HeliographWorkbench extends LitElement {
  @property() api = '/heliograph/api';
  @property({ attribute: false }) source?: WorkbenchSource;
  @state() private list: Summary[] = [];
  @state() private dictionaries: string[] = [];
  @state() private selected = '';
  @state() private creating = false;
  @state() private error = '';

  private get calls(): WorkbenchSource {
    return (this.source ??= new HttpWorkbenchSource(this.api));
  }

  override async connectedCallback() {
    super.connectedCallback();
    await this.refresh();
    this.selected ||= new URLSearchParams(location.search).get('correspondence') ?? this.list[0]?.key ?? '';
  }

  private async refresh() {
    try {
      [this.list, this.dictionaries] = await Promise.all([this.calls.list(), this.calls.dictionaries()]);
    } catch (e) {
      this.error = `The workbench could not reach Heliograph: ${(e as Error).message}`;
    }
  }

  override render() {
    return html`
      <div class="layout">
        <nav aria-label="Correspondence">
          <h2>Correspondence</h2>
          <ul>
            ${this.list.map(
              (c) => html`<li>
                <button class=${c.key === this.selected ? 'on' : ''} aria-current=${c.key === this.selected ? 'page' : 'false'} @click=${() => this.select(c.key)}>
                  ${c.name}<span class="muted">${c.latestVersion ? `version ${c.latestVersion}` : 'draft'}</span>
                </button>
              </li>`,
            )}
          </ul>
          <button @click=${() => (this.creating = true)}>New correspondence</button>
        </nav>
        <main>
          ${this.error ? html`<p role="alert" class="error">${this.error}</p>` : null}
          ${this.creating ? this.newForm() : this.selected
            ? html`<heliograph-correspondence .source=${this.calls} .key=${this.selected} @published=${this.refresh}></heliograph-correspondence>`
            : html`<p>Choose a correspondence, or start a new one.</p>`}
        </main>
      </div>`;
  }

  private newForm() {
    return html`
      <form @submit=${this.create}>
        <h2>New correspondence</h2>
        <p><label for="name">What it is called</label><input id="name" name="name" type="text" required placeholder="Decision letter" /></p>
        <p>
          <label for="key">Key</label>
          <input id="key" name="key" type="text" required pattern="[a-z][a-z0-9]*(-[a-z0-9]+)*" placeholder="decision-letter" />
          <span class="muted">Lower-case letters, digits and hyphens. Systems that send it use this.</span>
        </p>
        <p>
          <label for="dictionary">Dictionary</label>
          <select id="dictionary" name="dictionary">${this.dictionaries.map((d) => html`<option>${d}</option>`)}</select>
        </p>
        <div class="actions">
          <button class="primary" type="submit">Create</button>
          <button type="button" @click=${() => (this.creating = false)}>Cancel</button>
        </div>
      </form>`;
  }

  private create = async (e: SubmitEvent) => {
    e.preventDefault();
    const form = new FormData(e.target as HTMLFormElement);
    const key = String(form.get('key'));
    const outcome = await this.calls.save(key, { expectedRevision: 0, name: String(form.get('name')), dictionary: String(form.get('dictionary')) });
    if (!outcome.saved) {
      this.error = outcome.diagnostics[0]?.message ?? 'It could not be created. Is the key already in use?';
      return;
    }
    this.creating = false;
    await this.refresh();
    this.select(key);
  };

  private select(key: string) {
    this.selected = key;
    this.creating = false;
    this.error = '';
    history.replaceState(null, '', `?correspondence=${encodeURIComponent(key)}`);
  }

  static override styles = [
    shared,
    css`
      :host { display: block; }
      .layout { display: grid; grid-template-columns: 16rem minmax(0, 1fr); gap: 2rem; }
      @media (max-width: 50rem) { .layout { grid-template-columns: 1fr; } }
      nav h2 { font-size: 1.1rem; margin: 0 0 0.75rem; }
      nav ul { list-style: none; padding: 0; margin: 0 0 1rem; }
      nav li button { width: 100%; text-align: left; border: none; border-left: 4px solid transparent; color: var(--hg-text); display: flex; flex-direction: column; }
      nav li button.on { border-left-color: var(--hg-accent); background: var(--hg-subtle); font-weight: 700; }
      nav li .muted { font-size: 0.85rem; font-weight: 400; }
      .error { color: var(--hg-error); font-weight: 600; }
      input[type='text'] { width: 100%; max-width: 24rem; display: block; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-workbench': HeliographWorkbench;
  }
}
