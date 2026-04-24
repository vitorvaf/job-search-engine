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

  const email = typeof (body as { email?: unknown })?.email === "string" ? (body as { email: string }).email.trim() : "";
  if (!email) {
    return NextResponse.json({ message: "Email é obrigatório." }, { status: 400, headers: NO_STORE_HEADERS });
  }

  try {
    const response = await fetch(`${getBackendUrl()}/api/account/resend-verification`, {
      method: "POST",
      headers: getInternalApiHeaders(),
      body: JSON.stringify({ email }),
      cache: "no-store",
    });

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        payload ?? { message: "Erro ao reenviar verificação." },
        { status: response.status, headers: NO_STORE_HEADERS },
      );
    }

    return NextResponse.json(payload ?? { sent: true }, { status: response.status, headers: NO_STORE_HEADERS });
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao reenviar verificação.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500, headers: NO_STORE_HEADERS },
    );
  }
}
