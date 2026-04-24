import { NextResponse, type NextRequest } from "next/server";
import { getBackendUrl, getInternalApiHeaders, safeJson } from "@/lib/api-proxy";
import { resolveSessionUserId } from "@/lib/session-user-id";

type Params = {
  params: Promise<{ id: string }>;
};

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const NO_STORE_HEADERS = { "Cache-Control": "no-store" };

async function proxyFavoriteMutation(method: "PUT" | "DELETE", id: string, userId: string) {
  const response = await fetch(`${getBackendUrl()}/api/me/favorites/${id}`, {
    method,
    headers: getInternalApiHeaders({ "X-User-Id": userId }),
    cache: "no-store",
  });

  const payload = await safeJson<unknown>(response);

  if (!response.ok) {
    return NextResponse.json(
      payload ?? { message: "Erro ao atualizar favorito." },
      { status: response.status, headers: NO_STORE_HEADERS },
    );
  }

  return NextResponse.json(payload ?? { ok: true }, { status: response.status, headers: NO_STORE_HEADERS });
}

export async function PUT(request: NextRequest, { params }: Params) {
  const { id } = await params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ message: "ID inválido." }, { status: 400, headers: NO_STORE_HEADERS });
  }

  const userId = await resolveSessionUserId(request);
  if (!userId) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401, headers: NO_STORE_HEADERS });
  }

  try {
    return await proxyFavoriteMutation("PUT", id, userId);
  } catch (error) {
    return NextResponse.json(
      { message: "Falha interna ao favoritar vaga.", error: error instanceof Error ? error.message : "Unknown error" },
      { status: 500, headers: NO_STORE_HEADERS },
    );
  }
}

export async function DELETE(request: NextRequest, { params }: Params) {
  const { id } = await params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ message: "ID inválido." }, { status: 400, headers: NO_STORE_HEADERS });
  }

  const userId = await resolveSessionUserId(request);
  if (!userId) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401, headers: NO_STORE_HEADERS });
  }

  try {
    return await proxyFavoriteMutation("DELETE", id, userId);
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao remover favorito.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500, headers: NO_STORE_HEADERS },
    );
  }
}
