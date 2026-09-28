import { ExamplePrefix } from './ExamplePrefix.js';

// Herda tudo do ExamplePrefix e dobra o Power: variações de um prefixo sem
// repetir código, como o ExampleDerivedPrefix do tModLoader.
export class ExampleDerivedPrefix extends ExamplePrefix {
    get Power() { return super.Power * 2; }
}
