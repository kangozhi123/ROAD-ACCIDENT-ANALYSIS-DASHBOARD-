<?php
require_once __DIR__ . '/db.php';
session_start();
header('Content-Type: application/json');

$data = json_decode(file_get_contents('php://input'), true);
if (!$data) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid request payload']);
    exit;
}

if (empty($data['forceNumber']) || empty($data['password'])) {
    http_response_code(400);
    echo json_encode(['error' => 'Force number and password are required']);
    exit;
}

try {
    $pdo = getDatabaseConnection();
    $stmt = $pdo->prepare('SELECT id, full_name, force_number, email, password_hash, station FROM users WHERE force_number = :force_number');
    $stmt->execute([':force_number' => trim($data['forceNumber'])]);
    $user = $stmt->fetch();

    if (!$user || !password_verify($data['password'], $user['password_hash'])) {
        http_response_code(401);
        echo json_encode(['error' => 'Invalid credentials']);
        exit;
    }

    $_SESSION['user'] = [
        'id' => $user['id'],
        'full_name' => $user['full_name'],
        'force_number' => $user['force_number'],
        'email' => $user['email'],
        'station' => $user['station'],
    ];

    echo json_encode(['success' => true, 'message' => 'Login successful']);
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'Database error: ' . $e->getMessage()]);
}
