import { NextResponse, type NextRequest } from "next/server";
import { normalizeFavoriteJobsResponse } from "@/lib/normalizers";
import { getBackendUrl, getInternalApiHeaders, safeJson } from "@/lib/api-proxy";
import { resolveSessionUserId } from "@/lib/session-user-id";

const NO_STORE_HEADERS = { "Cache-Control": "no-store" };

export async function GET(request: NextRequest) {
  const userId = await resolveSessionUserId(request);

  if (!userId) {
    return NextResponse.json({ message: "Unauthorized" }, { status: 401, headers: NO_STORE_HEADERS });
  }

  try {
    const response = await fetch(`${getBackendUrl()}/api/me/favorites`, {
      method: "GET",
      headers: getInternalApiHeaders({ "X-User-Id": userId }),
      cache: "no-store",
    });

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        payload ?? { message: "Erro ao buscar favoritos." },
        { status: response.status, headers: NO_STORE_HEADERS },
      );
    }

    return NextResponse.json(normalizeFavoriteJobsResponse(payload), { headers: NO_STORE_HEADERS });
  } catch (error) {
    return NextResponse.json(
      { message: "Falha interna ao carregar favoritos.", error: error instanceof Error ? error.message : "Unknown error" },
      { status: 500, headers: NO_STORE_HEADERS },
    );
  }
}
