// A dictionary's JSON, turned into one flat row per field for editing, and back again. A list's own fields are rows
// that name the list they belong to.

export const fieldTypes = ['text', 'date', 'address', 'number', 'money', 'yesNo', 'list'] as const;

export interface FieldRow {
  name: string;
  key: string;
  type: string;
  list: string;
}

interface FieldJson {
  name: string;
  key: string;
  type: string;
  fields?: FieldJson[];
}

export function toRows(json: string): FieldRow[] {
  const fields: FieldJson[] = JSON.parse(json).fields ?? [];
  return fields.flatMap((field) => [
    { name: field.name, key: field.key, type: field.type, list: '' },
    ...(field.fields ?? []).map((inner) => ({ name: inner.name, key: inner.key, type: inner.type, list: field.name })),
  ]);
}

export function toJson(rows: FieldRow[]): string {
  const top = rows.filter((row) => !row.list);
  const fields = top.map((row) => {
    const own: FieldJson = { name: row.name.trim(), key: row.key.trim(), type: row.type };
    if (row.type === 'list') {
      own.fields = rows.filter((inner) => inner.list === row.name).map((inner) => ({ name: inner.name.trim(), key: inner.key.trim(), type: inner.type }));
    }
    return own;
  });
  return `${JSON.stringify({ fields }, null, 2)}\n`;
}

/** A key made from a name, as an author would expect: "Member name" becomes "memberName". */
export function keyFor(name: string): string {
  const words = name.trim().split(/[^A-Za-z0-9]+/).filter(Boolean);
  return words.map((word, i) => (i === 0 ? word.toLowerCase() : word[0].toUpperCase() + word.slice(1).toLowerCase())).join('');
}

/** A made-up recipient with a value of the right shape for each field, to start a new sample from. */
export function exampleSample(rows: FieldRow[]): string {
  const value = (type: string): unknown =>
    ({ date: '2027-06-19', address: ['1 Example Street', 'Exampletown', 'ZZ1 1ZZ'], number: 1, money: 10, yesNo: true })[type] ?? 'Example';
  const sample: Record<string, unknown> = {};
  for (const row of rows.filter((r) => !r.list)) {
    sample[row.key] =
      row.type === 'list'
        ? [Object.fromEntries(rows.filter((inner) => inner.list === row.name).map((inner) => [inner.key, value(inner.type)]))]
        : value(row.type);
  }
  return `${JSON.stringify(sample, null, 2)}\n`;
}
