import { NextResponse } from "next/server";
import { getBackendUrl, safeJson } from "@/lib/api-proxy";
import { normalizeJob } from "@/lib/normalizers";

type Params = {
  params: { id: string };
};

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export async function GET(_: Request, { params }: Params) {
  const { id } = await params;

  if (!UUID_RE.test(id)) {
    return NextResponse.json({ message: "ID inválido." }, { status: 400 });
  }

  try {
    const response = await fetch(`${getBackendUrl()}/api/jobs/${id}`, {
      headers: { Accept: "application/json" },
      next: { revalidate: 120 },
    });

    if (response.status === 404) {
      return NextResponse.json({ message: "Vaga não encontrada." }, { status: 404 });
    }

    const payload = await safeJson<unknown>(response);

    if (!response.ok) {
      return NextResponse.json(
        { message: "Erro ao buscar vaga no backend.", status: response.status, details: payload },
        { status: response.status },
      );
    }

    const normalized = normalizeJob(payload);

    return NextResponse.json({ ...normalized, id: normalized.id || id }, { headers: { "Cache-Control": "s-maxage=120" } });
  } catch (error) {
    return NextResponse.json(
      {
        message: "Falha interna ao consultar vaga.",
        error: error instanceof Error ? error.message : "Unknown error",
      },
      { status: 500 },
    );
  }
}
