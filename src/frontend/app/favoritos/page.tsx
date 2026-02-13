import { FavoritesList } from "@/components/favorites-list";

export default function FavoritosPage() {
  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="font-serif text-4xl text-ink md:text-5xl">Favoritos</h1>
        <p className="text-sm text-muted">Vagas salvas localmente para você revisitar sem esforço.</p>
      </header>
      <FavoritesList />
    </div>
  );
}
