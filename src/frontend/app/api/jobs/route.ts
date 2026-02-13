import { NextResponse, type NextRequest } from "next/server";
import { buildJobsQueryParams, getBackendUrl, safeJson } from "@/lib/api-proxy";
import { normalizeJobsResponse } from "@/lib/normalizers";

export async function GET(request: NextRequest) {
  try {
    const { query, page, pageSize } = buildJobsQueryParams(request);
    const backendUrl = `${getBackendUrl()}/api/jobs?${query.toString()}`;

    const response = await fetch(backendUrl, {
      headers: { Accept: "application/json" },
      next: { revalidate: 60 },
    });

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        { message: "Erro ao buscar vagas no backend.", status: response.status, details: payload },
        { status: response.status },
      );
    }

    const normalized = normalizeJobsResponse(payload, page, pageSize);

    return NextResponse.json(normalized, {
      headers: { "Cache-Control": "s-maxage=60, stale-while-revalidate=120" },
    });
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao consultar vagas.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500 },
    );
  }
}
