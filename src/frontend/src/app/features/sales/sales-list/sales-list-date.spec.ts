import { toLocalDateInput } from './sales-list';

describe('sales list date query mapping', () => {
  it('maps UTC range boundaries back to the local calendar date', () => {
    const localDate = '2026-09-24';
    const start = new Date(`${localDate}T00:00:00`).toISOString();
    const end = new Date(`${localDate}T23:59:59.999`).toISOString();

    expect(toLocalDateInput(start)).toBe(localDate);
    expect(toLocalDateInput(end)).toBe(localDate);
  });

  it('returns an empty value for a missing or invalid query parameter', () => {
    expect(toLocalDateInput(undefined)).toBe('');
    expect(toLocalDateInput('not-a-date')).toBe('');
  });
});
