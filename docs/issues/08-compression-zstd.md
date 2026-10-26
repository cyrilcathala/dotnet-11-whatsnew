# 08 — Compression zstd de bout en bout + stream adapters

Labels : `ready-for-agent` · Tag : `04-telemetry`

## Parent

[docs/spec.md](../spec.md)

## What to build

Le simulateur peut compresser ses envois en zstd (`--compression zstd`) grâce au nouveau `ZstandardCompressedContent`, qui pose `Content-Encoding: zstd` et compresse au fil de l'envoi. L'API décompresse avec le middleware `RequestDecompression` avant de lire le body via le `PipeReader`, quel que soit le format.

**Première chose à vérifier** : ASP.NET Core 11 fournit-il nativement un provider de décompression zstd ? Si ce n'est pas le cas, écrire un `IDecompressionProvider` maison basé sur `ZstandardStream`. Mettre à jour le ❓ de `docs/nouveautes-dotnet-11.md` selon le résultat.

Stream adapters : utiliser `ReadOnlySequenceStream` pour passer sans copie un segment du `PipeReader` à une API qui attend un `Stream`, à un endroit où c'est réellement utile dans le pipeline. Utiliser aussi `ReadOnlyMemoryStream` ou `StringStream` là où ils s'appliquent naturellement (simulateur, tests).

## Acceptance criteria

- [ ] `--compression zstd` fonctionne avec les formats JSONL, CBOR et JSON.
- [ ] Test via l'API : un import envoyé avec `ZstandardCompressedContent` produit le même état qu'un import non compressé.
- [ ] Un `Content-Encoding` non supporté est rejeté proprement.
- [ ] `ReadOnlySequenceStream` est utilisé sur le chemin d'import, sans copie du segment.
- [ ] Le ❓ sur la décompression zstd côté serveur est résolu dans `docs/nouveautes-dotnet-11.md`.

## Blocked by

- [05 — Télémétrie JSONL (tracer bullet)](05-telemetrie-jsonl-tracer.md)

User stories : 40, 50
