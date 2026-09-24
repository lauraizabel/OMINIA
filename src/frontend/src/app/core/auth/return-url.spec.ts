import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it('preserves internal deep links', () => {
    expect(safeReturnUrl('/sales/42?_page=2')).toBe('/sales/42?_page=2');
  });

  it.each([undefined, null, '', 'https://evil.example', '//evil.example', '/login?next=/sales'])(
    'falls back to sales for unsafe value %s',
    (value) => expect(safeReturnUrl(value)).toBe('/sales'),
  );
});
