<?php
function getDatabaseConnection(): PDO
{
    $dbFile = __DIR__ . '/database.sqlite';
    $dsn = 'sqlite:' . $dbFile;
    $options = [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
    ];

    $pdo = new PDO($dsn, null, null, $options);
    createTables($pdo);
    seedTestUser($pdo);
    return $pdo;
}

function createTables(PDO $pdo): void
{
    $pdo->exec(
        'CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            full_name TEXT NOT NULL,
            force_number TEXT NOT NULL UNIQUE,
            email TEXT NOT NULL UNIQUE,
            password_hash TEXT NOT NULL,
            station TEXT NOT NULL,
            created_at TEXT NOT NULL
        )'
    );
}

function seedTestUser(PDO $pdo): void
{
    // Insert a friendly test user for local development if it doesn't exist
    $check = $pdo->prepare('SELECT id FROM users WHERE force_number = :force');
    $check->execute([':force' => 'ZP-00001']);
    if ($check->fetch()) {
        return;
    }

    $passwordHash = password_hash('Password123!', PASSWORD_DEFAULT);
    $stmt = $pdo->prepare('INSERT INTO users (full_name, force_number, email, password_hash, station, created_at) VALUES (:full_name, :force_number, :email, :password_hash, :station, :created_at)');
    $stmt->execute([
        ':full_name' => 'Test Officer',
        ':force_number' => 'ZP-00001',
        ':email' => 'test.officer@police.gov.zm',
        ':password_hash' => $passwordHash,
        ':station' => 'kitwe_central',
        ':created_at' => date('Y-m-d H:i:s'),
    ]);
}
