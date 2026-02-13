import { NextResponse } from "next/server";
import { getBackendUrl, safeJson } from "@/lib/api-proxy";
import { normalizeSources } from "@/lib/normalizers";

export async function GET() {
  try {
    const response = await fetch(`${getBackendUrl()}/api/sources`, {
      headers: { Accept: "application/json" },
      next: { revalidate: 600 },
    });

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        { message: "Erro ao buscar fontes no backend.", status: response.status, details: payload },
        { status: response.status },
      );
    }

    return NextResponse.json(normalizeSources(payload), {
      headers: { "Cache-Control": "s-maxage=600, stale-while-revalidate=1200" },
    });
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao consultar fontes.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500 },
    );
  }
}
