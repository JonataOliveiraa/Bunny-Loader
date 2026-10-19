import { ExampleStructure } from '../../Content/Structures/ExampleStructure.js';

// Põe o castelo de exemplo na geração do mundo. Como o Calamity, a etapa
// nova entra logo depois de uma etapa do jogo achada pelo nome: depois dos
// Girassóis o terreno da superfície já está pronto, e as árvores, as plantas
// e os microbiomas que vêm depois respeitam a área protegida do castelo.
export class ExampleStructureSystem extends ModSystem {
    ModifyWorldGenTasks(tasks) {
        const pass = new PassLegacy('Example Structure', (progress, configuration) => {
            progress.Message = ModLocalization.Translate('WorldGen.ExampleStructure');
            ExampleStructure.Generate();
        });

        // Se o nome não existir (outra versão do jogo), fica antes da limpeza
        // final, ou no fim da lista.
        const after = tasks.findIndex((task) => task.Name === 'Sunflowers');
        const before = tasks.findIndex((task) => task.Name === 'Final Cleanup');
        if (after >= 0) tasks.splice(after + 1, 0, pass);
        else if (before >= 0) tasks.splice(before, 0, pass);
        else tasks.push(pass);
    }
}
