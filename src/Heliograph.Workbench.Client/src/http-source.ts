import { HttpDictionaryCalls, enc, ok } from './http-dictionary-calls.js';
import type { DraftChange, EditLink, FileKind, Parts, Problem, SaveOutcome, Summary, WorkbenchSource, Draft, DictionaryEntry } from './source.js';

/** The workbench's calls over Heliograph.Api. */
export class HttpWorkbenchSource extends HttpDictionaryCalls implements WorkbenchSource {

  list = () => this.json<Summary[]>('GET', 'correspondence');
  dictionaries = () => this.json<string[]>('GET', 'dictionaries');
  dictionary = async (name: string) => (await this.json<{ fields: DictionaryEntry[] }>('GET', `dictionaries/${enc(name)}`)).fields;
  samples = (dictionary: string) => this.json<string[]>('GET', `dictionaries/${enc(dictionary)}/samples`);

  async draft(key: string): Promise<Draft | null> {
    const response = await this.send(this.url(`correspondence/${enc(key)}`));
    return response.status === 404 ? null : ok(response).json();
  }

  save = (key: string, change: DraftChange) =>
    this.saving(this.send(this.url(`correspondence/${enc(key)}`), jsonBody('PUT', change)));

  upload = (key: string, expectedRevision: number, file: Blob) =>
    this.saving(this.send(this.url(`correspondence/${enc(key)}/document?expectedRevision=${expectedRevision}`), { method: 'PUT', body: file }));

  check = async (key: string) => (await this.json<{ diagnostics: Problem[] }>('POST', `correspondence/${enc(key)}/check`)).diagnostics;
  preview = (key: string, sample: string) => this.json<Parts>('POST', `correspondence/${enc(key)}/preview`, { sample });

  async publish(key: string) {
    const response = await this.send(this.url(`correspondence/${enc(key)}/publish`), jsonBody('POST', {}));
    return response.json();
  }

  async editLink(key: string): Promise<EditLink | null> {
    const response = await this.send(this.url(`correspondence/${enc(key)}/document/edit-link`), { method: 'POST' });
    return response.ok ? response.json() : null;
  }

  async file(key: string, which: FileKind, sample?: string): Promise<Blob | null> {
    const path = `correspondence/${enc(key)}/`;
    const response = which.startsWith('preview')
      ? await this.send(this.url(path + (which === 'preview-pdf' ? 'preview/pdf' : 'preview/document')), jsonBody('POST', { sample }))
      : await this.send(this.url(path + (which === 'document' ? 'document' : `document/${which}`)));
    return response.ok ? response.blob() : null;
  }

  private async saving(call: Promise<Response>): Promise<SaveOutcome> {
    const response = await call;
    const body = await response.json();
    return { saved: response.ok, revision: body.revision ?? null, diagnostics: body.diagnostics ?? [], conflict: response.status === 409 };
  }

  private async json<T>(method: string, path: string, body?: unknown): Promise<T> {
    return ok(await this.send(this.url(path), body === undefined ? { method } : jsonBody(method, body))).json();
  }

}

const jsonBody = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
});
