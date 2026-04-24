import { NextResponse } from "next/server";
import { getBackendUrl, getInternalApiHeaders, safeJson } from "@/lib/api-proxy";

const NO_STORE_HEADERS = { "Cache-Control": "no-store" };

export async function POST(request: Request) {
  let body: unknown;

  try {
    body = (await request.json()) as unknown;
  } catch {
    return NextResponse.json({ message: "Payload inválido." }, { status: 400, headers: NO_STORE_HEADERS });
  }

  const token = typeof (body as { token?: unknown })?.token === "string" ? (body as { token: string }).token.trim() : "";
  const newPassword =
    typeof (body as { newPassword?: unknown })?.newPassword === "string"
      ? (body as { newPassword: string }).newPassword
      : "";

  if (!token || !newPassword) {
    return NextResponse.json(
      { message: "Token e nova senha são obrigatórios." },
      { status: 400, headers: NO_STORE_HEADERS },
    );
  }

  try {
    const response = await fetch(`${getBackendUrl()}/api/account/password/reset`, {
      method: "POST",
      headers: getInternalApiHeaders(),
      body: JSON.stringify({ token, newPassword }),
      cache: "no-store",
    });

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        payload ?? { message: "Erro ao redefinir senha." },
        { status: response.status, headers: NO_STORE_HEADERS },
      );
    }

    return NextResponse.json(payload ?? { reset: true }, { status: response.status, headers: NO_STORE_HEADERS });
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao redefinir senha.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500, headers: NO_STORE_HEADERS },
    );
  }
}
