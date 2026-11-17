import { LitElement, css, html } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { type FieldRow, exampleSample, fieldTypes, keyFor, toJson, toRows } from './dictionary-fields.js';
import './problems-list.js';
import type { DictionarySave, WorkbenchSource } from './source.js';
import { shared } from './styles.js';

/**
 * One dictionary: the fields a correspondence may use, each a tag an author writes, and the made-up recipients
 * previews use. Saving says what the change means for every draft that uses the dictionary; published versions keep
 * the dictionary they were published with.
 */
@customElement('heliograph-dictionary')
export class DictionaryView extends LitElement {
  @property({ attribute: false }) source!: WorkbenchSource;
  @property() name = '';
  @property({ type: Boolean }) editable = false;
  @state() private rows: FieldRow[] = [];
  @state() private result: DictionarySave | null = null;
  @state() private samples: string[] = [];
  @state() private sample = '';
  @state() private sampleText = '';
  @state() private status = '';

  override async willUpdate(changed: Map<string, unknown>) {
    if (changed.has('name') && this.name) {
      this.rows = toRows((await this.source.dictionaryDefinition(this.name)) ?? '{ "fields": [] }');
      this.result = null;
      await this.loadSamples(this.samples[0]);
    }
  }

  private async loadSamples(choose?: string) {
    this.samples = await this.source.samples(this.name).catch(() => []);
    this.sample = choose && this.samples.includes(choose) ? choose : (this.samples[0] ?? '');
    this.sampleText = this.sample ? ((await this.source.sample(this.name, this.sample)) ?? '') : '';
  }

  override render() {
    const lists = this.rows.filter((row) => row.type === 'list' && !row.list).map((row) => row.name);
    return html`
      <h2>${this.name}</h2>
      <p class="muted">
        Each field is something a correspondence may use: authors write it as a tag, <code>((Field name))</code>, and the data
        carries it under its key. ${this.editable ? '' : 'This site takes its dictionaries from elsewhere, so they cannot be changed here.'}
      </p>
      <table>
        <thead><tr><th scope="col">Field name</th><th scope="col">Key in the data</th><th scope="col">Type</th><th scope="col">In list</th><th scope="col"><span class="muted">Remove</span></th></tr></thead>
        <tbody>${this.rows.map((row, i) => this.row(row, i, lists))}</tbody>
      </table>
      ${this.editable
        ? html`<div class="actions">
            <button @click=${() => (this.rows = [...this.rows, { name: '', key: '', type: 'text', list: '' }])}>Add a field</button>
            <button class="primary" @click=${this.save}>Save the dictionary</button>
          </div>`
        : null}
      ${this.result ? this.outcome(this.result) : null}
      ${this.samplesSection()}`;
  }

  private row(row: FieldRow, i: number, lists: string[]) {
    const change = (patch: Partial<FieldRow>) => (this.rows = this.rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
    const disabled = !this.editable;
    return html`<tr>
      <td><input type="text" aria-label="Field name" .value=${row.name} ?disabled=${disabled}
        @input=${(e: Event) => {
          const name = (e.target as HTMLInputElement).value;
          change(row.key === '' || row.key === keyFor(row.name) ? { name, key: keyFor(name) } : { name });
        }} /></td>
      <td><input type="text" aria-label="Key in the data" .value=${row.key} ?disabled=${disabled} @input=${(e: Event) => change({ key: (e.target as HTMLInputElement).value })} /></td>
      <td><select aria-label="Type" ?disabled=${disabled} @change=${(e: Event) => change({ type: (e.target as HTMLSelectElement).value })}>
        ${fieldTypes.map((type) => html`<option value=${type} ?selected=${row.type === type}>${type === 'yesNo' ? 'yes or no' : type}</option>`)}
      </select></td>
      <td><select aria-label="In list" ?disabled=${disabled || row.type === 'list'} @change=${(e: Event) => change({ list: (e.target as HTMLSelectElement).value })}>
        <option value="" ?selected=${!row.list}>—</option>
        ${lists.map((list) => html`<option ?selected=${row.list === list}>${list}</option>`)}
      </select></td>
      <td>${this.editable ? html`<button aria-label="Remove ${row.name}" @click=${() => (this.rows = this.rows.filter((_, j) => j !== i))}>Remove</button>` : null}</td>
    </tr>`;
  }

  private outcome(result: DictionarySave) {
    if (!result.saved) return html`<heliograph-problems .problems=${result.problems}></heliograph-problems>`;
    const broken = result.uses.filter((use) => use.diagnostics.some((d) => d.severity === 'Error'));
    return html`<div role="status" class="outcome">
      <p><strong>Saved.</strong> ${result.uses.length === 0 ? 'No correspondence uses this dictionary yet.' : `${result.uses.length} correspondence use it; ${broken.length === 0 ? 'every draft still checks out.' : `${broken.length} now ${broken.length === 1 ? 'has a problem' : 'have problems'}:`}`}</p>
      ${broken.map((use) => html`<h3>${use.name}</h3><heliograph-problems .problems=${use.diagnostics}></heliograph-problems>`)}
      <p class="muted">Published versions keep the dictionary they were published with, so nothing already sent changes.</p>
    </div>`;
  }

  private samplesSection() {
    return html`
      <h3>Sample recipients</h3>
      <p class="muted">Made-up people that previews use. Never put a real person here.</p>
      <div class="actions">
        <select aria-label="Sample" @change=${async (e: Event) => this.loadSamples((e.target as HTMLSelectElement).value)}>
          ${this.samples.map((s) => html`<option ?selected=${s === this.sample}>${s}</option>`)}
        </select>
        ${this.editable ? html`<button @click=${this.newSample}>New sample</button>` : null}
      </div>
      ${this.sample || this.sampleText
        ? html`<label for="sample-json">${this.sample || 'New sample'}</label>
            <textarea id="sample-json" .value=${this.sampleText} ?disabled=${!this.editable} @input=${(e: Event) => (this.sampleText = (e.target as HTMLTextAreaElement).value)}></textarea>
            ${this.editable
              ? html`<div class="actions">
                  <button class="primary" @click=${this.saveSample}>Save the sample</button>
                  ${this.samples.includes(this.sample) ? html`<button @click=${this.deleteSample}>Delete the sample</button>` : null}
                  <span class="status muted" role="status">${this.status}</span>
                </div>`
              : null}`
        : null}`;
  }

  private save = async () => {
    this.result = await this.source.saveDictionary(this.name, toJson(this.rows));
    this.dispatchEvent(new CustomEvent('dictionary-saved', { bubbles: true, composed: true }));
  };

  private newSample = () => {
    const name = prompt('A name for the sample, such as "approved"')?.trim();
    if (!name) return;
    this.sample = name;
    this.sampleText = exampleSample(this.rows);
  };

  private saveSample = async () => {
    const problems = await this.source.saveSample(this.name, this.sample, this.sampleText);
    this.status = problems.length === 0 ? 'Saved.' : problems[0].message;
    if (problems.length === 0) await this.loadSamples(this.sample);
  };

  private deleteSample = async () => {
    await this.source.deleteSample(this.name, this.sample);
    this.status = 'Deleted.';
    await this.loadSamples();
  };

  static override styles = [
    shared,
    css`
      h2 { margin: 0 0 0.5rem; font-size: 1.5rem; }
      h3 { margin: 1.5rem 0 0.5rem; font-size: 1.1rem; }
      td input[type='text'] { width: 100%; box-sizing: border-box; }
      .outcome { margin-top: 1rem; }
      textarea { min-height: 14rem; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-dictionary': DictionaryView;
  }
}
