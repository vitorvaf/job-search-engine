import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { FavoritesList } from "@/components/favorites-list";
import { LegacyFavoritesImport } from "@/components/legacy-favorites-import";

export const dynamic = "force-dynamic";

export default async function FavoritosPage() {
  const session = await auth();

  if (!session) {
    redirect(`/entrar?callbackUrl=${encodeURIComponent("/favoritos")}`);
  }

  const userId = typeof session.user?.id === "string" ? session.user.id.trim() : "";

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="font-serif text-4xl text-ink md:text-5xl">Favoritos</h1>
        <p className="text-sm text-muted">Vagas salvas na sua conta para você revisitar sem esforço.</p>
      </header>
      <LegacyFavoritesImport userId={userId} />
      <FavoritesList />
    </div>
  );
}
