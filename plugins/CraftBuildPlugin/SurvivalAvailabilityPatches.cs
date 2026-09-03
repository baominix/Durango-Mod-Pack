namespace BaoX.DurangoOriginal.CraftBuildMod
{
    // Intentionally no Harmony patches here.
    //
    // Original RecipeSystem treats Recipes.Ids / ArtifactBlueprints.Ids from the
    // Frontend as authoritative. Rewriting OnRecipeListMsg/OnBlueprintListMsg on
    // the client broke external-emulator responses and duplicated the offline
    // backend policy. Availability is now computed only by CraftBuildBackend when
    // it owns the local offline response.
}
