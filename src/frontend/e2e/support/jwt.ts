import { createHmac } from 'node:crypto';

export function expiredToken(validToken: string): string {
  const secret = process.env['E2E_JWT_SECRET'];
  if (!secret) {
    throw new Error('E2E_JWT_SECRET is required to exercise a genuinely expired token.');
  }

  const [encodedHeader, encodedPayload] = validToken.split('.');
  const payload = JSON.parse(Buffer.from(encodedPayload, 'base64url').toString('utf8')) as Record<
    string,
    unknown
  >;
  const now = Math.floor(Date.now() / 1000);
  const expiredPayload = Buffer.from(
    JSON.stringify({ ...payload, iat: now - 1_200, nbf: now - 1_200, exp: now - 600 }),
  ).toString('base64url');
  const unsigned = `${encodedHeader}.${expiredPayload}`;
  const signature = createHmac('sha256', secret).update(unsigned).digest('base64url');
  return `${unsigned}.${signature}`;
}
