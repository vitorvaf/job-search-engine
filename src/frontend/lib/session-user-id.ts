import type { NextRequest } from "next/server";
import { getToken } from "next-auth/jwt";
import { auth } from "@/auth";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function sanitizeUserId(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  return UUID_RE.test(trimmed) ? trimmed : null;
}

export async function resolveSessionUserId(request?: NextRequest): Promise<string | null> {
  const session = await auth();
  const sessionUserId = sanitizeUserId(session?.user?.id);
  if (sessionUserId) {
    return sessionUserId;
  }

  if (!request) {
    return null;
  }

  const token = await getToken({ req: request, secret: process.env.AUTH_SECRET });
  const tokenUserId = sanitizeUserId(token?.userId) ?? sanitizeUserId(token?.sub);
  return tokenUserId;
}
