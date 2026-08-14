<?php
require_once __DIR__ . '/db.php';

$pdo = getDatabaseConnection();

// Check if test user exists
$stmt = $pdo->prepare('SELECT id FROM users WHERE force_number = :force');
$stmt->execute([':force' => 'ZP-00001']);
if ($stmt->fetch()) {
    echo "Test user already exists.\n";
    exit;
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

echo "Seeded test user: force_number=ZP-00001 password=Password123!\n";
