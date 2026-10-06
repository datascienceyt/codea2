// Comprueba que los JSON de telemetría de la documentación tienen EXACTAMENTE los campos que
// declara Assets/_Main/Scripts/Telemetry/TelemetryData.cs, en el mismo orden y con el mismo
// tipo. Es lo que emite JsonUtility: todos los campos, heredados primero, en orden de
// declaración.
//
//   node Docs/Herramientas/validar_json_telemetria.js            → los de la documentación
//   node Docs/Herramientas/validar_json_telemetria.js run.json   → uno sacado del visor
//
// Los valores "<VARIABLE: ...>" del JSON esperado del plan de prueba se aceptan en cualquier campo.

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..', '..');
const model = fs.readFileSync(path.join(root, 'Assets/_Main/Scripts/Telemetry/TelemetryData.cs'), 'utf8');

// --- Modelo: clase → { base, campos[] } ---

const classes = {};
const classPattern = /public class (\w+)(?:\s*:\s*(\w+))?\s*\{([\s\S]*?)\n\}/g;

for (const match of model.matchAll(classPattern)) {
    const fields = [];
    const fieldPattern = /^\s*public\s+([\w<>]+)\s+(\w+)\s*(?:=[^;]*)?;/gm;

    for (const field of match[3].matchAll(fieldPattern))
        fields.push({ type: field[1], name: field[2] });

    classes[match[1]] = { base: match[2], fields };
}

function fieldsOf(name) {
    const cls = classes[name];
    if (!cls) throw new Error(`TelemetryData.cs no declara la clase '${name}'`);

    return (cls.base ? fieldsOf(cls.base) : []).concat(cls.fields);
}

// --- Validación ---

const isPlaceholder = value => typeof value === 'string' && value.startsWith('<VARIABLE');

function check(value, type, where, errors) {
    if (isPlaceholder(value)) return;

    const list = type.match(/^List<(\w+)>$/);

    if (list) {
        if (!Array.isArray(value)) { errors.push(`${where}: debería ser una lista`); return; }
        value.forEach((item, i) => check(item, list[1], `${where}[${i}]`, errors));
        return;
    }

    if (type === 'string') { if (typeof value !== 'string') errors.push(`${where}: debería ser texto`); return; }
    if (type === 'bool') { if (typeof value !== 'boolean') errors.push(`${where}: debería ser true/false, es ${JSON.stringify(value)}`); return; }
    if (type === 'int') { if (!Number.isInteger(value)) errors.push(`${where}: debería ser un entero, es ${JSON.stringify(value)}`); return; }
    if (type === 'float') { if (typeof value !== 'number') errors.push(`${where}: debería ser un número, es ${JSON.stringify(value)}`); return; }

    if (value === null || typeof value !== 'object' || Array.isArray(value)) {
        errors.push(`${where}: debería ser un objeto ${type}`);
        return;
    }

    const expected = fieldsOf(type);
    const names = expected.map(f => f.name);
    const actual = Object.keys(value);

    for (const name of names) if (!actual.includes(name)) errors.push(`${where}: falta '${name}'`);
    for (const name of actual) if (!names.includes(name)) errors.push(`${where}: sobra '${name}'`);

    const common = actual.filter(name => names.includes(name));
    const ordered = names.filter(name => actual.includes(name));
    if (common.join() !== ordered.join())
        errors.push(`${where}: orden de campos distinto al del modelo (${ordered.join(', ')})`);

    for (const field of expected)
        if (field.name in value) check(value[field.name], field.type, `${where}.${field.name}`, errors);
}

function listJson(dir) {
    return fs.readdirSync(dir).filter(f => f.endsWith('.json')).map(f => path.join(dir, f));
}

const files = process.argv.length > 2
    ? process.argv.slice(2).map(f => path.resolve(f))
    : [path.join(root, 'Docs/ejemplo_run_telemetria.json')]
        .concat(listJson(path.join(root, 'Docs/Pruebas')))
        .concat(listJson(path.join(root, 'Docs/JSON Samples')));

let failed = 0;

for (const file of files) {
    const errors = [];

    try {
        check(JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, '')), 'RunRecord', 'raíz', errors);
    } catch (error) {
        errors.push(`no se pudo leer: ${error.message}`);
    }

    const name = path.relative(root, file);

    if (errors.length === 0) {
        console.log(`OK     ${name}`);
        continue;
    }

    failed++;
    console.log(`FALLA  ${name}`);
    for (const error of errors.slice(0, 20)) console.log(`         ${error}`);
    if (errors.length > 20) console.log(`         ... y ${errors.length - 20} más`);
}

process.exit(failed === 0 ? 0 : 1);
