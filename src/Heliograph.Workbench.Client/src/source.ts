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

export interface EditLink {
  url: string;
  word: string;
  libreOffice: string;
  expires: string;
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
  /** Addresses that open the draft's document in a word processor and save it straight back. Null when there is no document. */
  editLink(key: string): Promise<EditLink | null>;
  /** A file to save: the document, a starter, the document with comments, a rendered preview or its PDF. Null when there is none. */
  file(key: string, which: FileKind, sample?: string): Promise<Blob | null>;
}

export type FileKind = 'document' | 'starter' | 'annotated' | 'preview-document' | 'preview-pdf';
