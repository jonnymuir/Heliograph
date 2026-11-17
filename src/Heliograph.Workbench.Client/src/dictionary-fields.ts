// A dictionary's JSON as the rows the Dictionaries screen edits, and back again. A list is a field that repeats, such
// as payments; its own fields (each payment's date and amount) sit inside it.

export const fieldTypes = ['text', 'date', 'address', 'number', 'money', 'yesNo', 'list'] as const;

/** The types a field inside a list may have: a list cannot hold another list. */
export const innerFieldTypes = fieldTypes.filter((type) => type !== 'list');

export interface FieldRow {
  name: string;
  key: string;
  type: string;
  fields: FieldRow[];
}

interface FieldJson {
  name: string;
  key: string;
  type: string;
  fields?: FieldJson[];
}

const fromJson = (field: FieldJson): FieldRow => ({ name: field.name, key: field.key, type: field.type, fields: (field.fields ?? []).map(fromJson) });

const toFieldJson = (row: FieldRow): FieldJson => {
  const own: FieldJson = { name: row.name.trim(), key: row.key.trim(), type: row.type };
  if (row.type === 'list') own.fields = row.fields.map(toFieldJson);
  return own;
};

export const toRows = (json: string): FieldRow[] => (JSON.parse(json).fields ?? []).map(fromJson);

export const toJson = (rows: FieldRow[]): string => `${JSON.stringify({ fields: rows.map(toFieldJson) }, null, 2)}\n`;

export const emptyRow = (): FieldRow => ({ name: '', key: '', type: 'text', fields: [] });

/** A key made from a name, as an author would expect: "Member name" becomes "memberName". */
export function keyFor(name: string): string {
  const words = name.trim().split(/[^A-Za-z0-9]+/).filter(Boolean);
  return words.map((word, i) => (i === 0 ? word.toLowerCase() : word[0].toUpperCase() + word.slice(1).toLowerCase())).join('');
}

/** A made-up recipient with a value of the right shape for each field, to start a new sample from. */
export function exampleSample(rows: FieldRow[]): string {
  const value = (row: FieldRow): unknown =>
    row.type === 'list'
      ? [Object.fromEntries(row.fields.map((inner) => [inner.key, value(inner)]))]
      : (({ date: '2027-06-19', address: ['1 Example Street', 'Exampletown', 'ZZ1 1ZZ'], number: 1, money: 10, yesNo: true }) as Record<string, unknown>)[row.type] ?? 'Example';
  return `${JSON.stringify(Object.fromEntries(rows.map((row) => [row.key, value(row)])), null, 2)}\n`;
}
