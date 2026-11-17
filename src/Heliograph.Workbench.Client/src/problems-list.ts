import { LitElement, css, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { Problem } from './source.js';
import { shared } from './styles.js';

/** Every problem with the correspondence, each with where it is and what to do: the same words an agent is given. */
@customElement('heliograph-problems')
export class ProblemsList extends LitElement {
  @property({ attribute: false }) problems: Problem[] = [];

  override render() {
    if (this.problems.length === 0) {
      return html`<p class="clear" role="status">No problems found.</p>`;
    }
    const errors = this.problems.filter((p) => p.severity === 'Error').length;
    return html`
      <h3>${errors > 0 ? `${errors} ${errors === 1 ? 'problem stops' : 'problems stop'} this being published` : 'Things to look at'}</h3>
      <ul>
        ${this.problems.map(
          (p) => html`<li class=${p.severity === 'Error' ? 'error' : 'warning'}>
            <strong>${p.severity === 'Error' ? 'Error' : 'Warning'}</strong>
            ${p.location ? html`<span class="where">${where(p.location)}</span>` : null}
            <p>${p.message}</p>
            <p class="fix">${p.fix} <code class="muted">${p.code}</code></p>
          </li>`,
        )}
      </ul>`;
  }

  static override styles = [
    shared,
    css`
      ul { list-style: none; padding: 0; margin: 0; }
      li { border-left: 5px solid var(--hg-warning); padding: 0.5rem 0.75rem; margin-bottom: 0.5rem; background: var(--hg-subtle); }
      li.error { border-left-color: var(--hg-error); }
      li p { margin: 0.25rem 0; }
      .where { color: var(--hg-muted); margin-left: 0.5rem; }
      .fix { font-weight: 600; }
      .clear { color: var(--hg-ok); font-weight: 600; }
      h3 { margin: 0 0 0.5rem; font-size: 1.05rem; }
    `,
  ];
}

const where = (l: { part: string; paragraph: number; excerpt: string }) =>
  `In the ${l.part}, ${l.part.startsWith('email') || l.part === 'sms' ? 'line' : 'paragraph'} ${l.paragraph}: “${l.excerpt}”`;

declare global {
  interface HTMLElementTagNameMap {
    'heliograph-problems': ProblemsList;
  }
}
