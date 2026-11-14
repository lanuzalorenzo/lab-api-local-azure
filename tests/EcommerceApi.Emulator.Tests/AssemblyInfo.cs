// La fábrica ajusta `Publisher__Mode` en el entorno del proceso: una prueba a la
// vez para que el modo de una no decida el de otra.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
