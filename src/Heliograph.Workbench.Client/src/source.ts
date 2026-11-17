// Everything the workbench asks of its host, in one place. The standalone page talks to Heliograph.Api over HTTP;
// another host (the Umbraco backoffice) supplies the same calls against its own endpoints and sign-in.

export interface Problem {
  code: string;
  message: string;
  fix: string;
  location?: { part: string; paragraph: number; excerpt: string } | null;
  severity: 'Error' | 'Warning';
}

export interface Summary {
  key: string;
  name: string;
  dictionary: string;
  latestVersion: number | null;
}

export interface Draft {
  key: string;
  revision: number;
  name: string;
  dictionary: string;
  email: string | null;
  sms: string | null;
  hasDocument: boolean;
  latestVersion: number | null;
}

export interface DictionaryEntry {
  tag: string | null;
  name: string;
  key: string;
  type: string;
  list: string | null;
}

export interface SaveOutcome {
  saved: boolean;
  revision: number | null;
  diagnostics: Problem[];
  conflict: boolean;
}

export interface Parts {
  version: number | null;
  email: { subject: string; html: string; text: string } | null;
  sms: { text: string; encoding: string; segments: number } | null;
  hasDocument: boolean;
  diagnostics: Problem[];
}

export interface DraftChange {
  expectedRevision: number;
  name?: string;
  dictionary?: string;
  email?: string;
  sms?: string;
  remove?: Array<'document' | 'email' | 'sms'>;
}

export interface WorkbenchSource {
  list(): Promise<Summary[]>;
  dictionaries(): Promise<string[]>;
  dictionary(name: string): Promise<DictionaryEntry[]>;
  samples(dictionary: string): Promise<string[]>;
  draft(key: string): Promise<Draft | null>;
  save(key: string, change: DraftChange): Promise<SaveOutcome>;
  upload(key: string, expectedRevision: number, file: Blob): Promise<SaveOutcome>;
  check(key: string): Promise<Problem[]>;
  preview(key: string, sample: string): Promise<Parts>;
  publish(key: string): Promise<{ version: number | null; diagnostics: Problem[] }>;
  /** A file to save: the document, a starter, the document with comments, a rendered preview or its PDF. Null when there is none. */
  file(key: string, which: FileKind, sample?: string): Promise<Blob | null>;
}

export type FileKind = 'document' | 'starter' | 'annotated' | 'preview-document' | 'preview-pdf';

/** The workbench's calls over Heliograph.Api. */
export class HttpWorkbenchSource implements WorkbenchSource {
  constructor(
    private readonly base: string,
    private readonly send: typeof fetch = (input, init) => fetch(input, { credentials: 'same-origin', ...init }),
  ) {}

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

  private url = (path: string) => `${this.base.replace(/\/$/, '')}/${path}`;
}

const enc = encodeURIComponent;

const jsonBody = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
});

function ok(response: Response): Response {
  if (!response.ok) {
    throw new Error(`The server answered ${response.status}.`);
  }
  return response;
}
