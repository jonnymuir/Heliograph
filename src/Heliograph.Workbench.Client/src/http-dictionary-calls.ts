import type { DictionarySave, DictionarySettings, Problem } from './source.js';

/** The workbench's calls about dictionaries over Heliograph.Api, and the plumbing every call shares. */
export class HttpDictionaryCalls {
  constructor(
    protected readonly base: string,
    protected readonly send: typeof fetch = (input, init) => fetch(input, { credentials: 'same-origin', ...init }),
  ) {}

  dictionarySettings = async (): Promise<DictionarySettings> => ok(await this.send(this.url('dictionary-settings'))).json();

  dictionaryDefinition = (name: string) => this.text(`dictionaries/${enc(name)}/definition`);

  sample = (dictionary: string, sample: string) => this.text(`dictionaries/${enc(dictionary)}/samples/${enc(sample)}`);

  async saveDictionary(name: string, json: string): Promise<DictionarySave> {
    const response = await this.send(this.url(`dictionaries/${enc(name)}`), { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: json });
    const body = await response.json();
    return { saved: response.ok, problems: body.problems ?? [], uses: body.uses ?? [] };
  }

  async saveSample(dictionary: string, sample: string, json: string): Promise<Problem[]> {
    const response = await this.send(this.url(`dictionaries/${enc(dictionary)}/samples/${enc(sample)}`), {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: json,
    });
    return response.ok ? [] : ((await response.json()).problems ?? []);
  }

  async deleteSample(dictionary: string, sample: string): Promise<void> {
    await this.send(this.url(`dictionaries/${enc(dictionary)}/samples/${enc(sample)}`), { method: 'DELETE' });
  }

  private async text(path: string): Promise<string | null> {
    const response = await this.send(this.url(path));
    return response.ok ? response.text() : null;
  }

  protected url = (path: string) => `${this.base.replace(/\/$/, '')}/${path}`;
}

export const enc = encodeURIComponent;

export function ok(response: Response): Response {
  if (!response.ok) {
    throw new Error(`The server answered ${response.status}.`);
  }
  return response;
}
