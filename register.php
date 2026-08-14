<?php
require_once __DIR__ . '/db.php';
header('Content-Type: application/json');

$data = json_decode(file_get_contents('php://input'), true);
if (!$data) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid request payload']);
    exit;
}

$required = ['fullName', 'newForceNumber', 'newEmail', 'newPassword', 'confirmPassword', 'newStation'];
foreach ($required as $field) {
    if (empty($data[$field])) {
        http_response_code(400);
        echo json_encode(['error' => 'Missing field: ' . $field]);
        exit;
    }
}

if ($data['newPassword'] !== $data['confirmPassword']) {
    http_response_code(400);
    echo json_encode(['error' => 'Passwords do not match']);
    exit;
}

try {
    $pdo = getDatabaseConnection();
    $stmt = $pdo->prepare('INSERT INTO users (full_name, force_number, email, password_hash, station, created_at) VALUES (:full_name, :force_number, :email, :password_hash, :station, :created_at)');
    $stmt->execute([
        ':full_name' => trim($data['fullName']),
        ':force_number' => trim($data['newForceNumber']),
        ':email' => trim($data['newEmail']),
        ':password_hash' => password_hash($data['newPassword'], PASSWORD_DEFAULT),
        ':station' => trim($data['newStation']),
        ':created_at' => date('Y-m-d H:i:s'),
    ]);
    echo json_encode(['success' => true, 'message' => 'Account created successfully']);
} catch (PDOException $e) {
    if ($e->getCode() === '23000') {
        http_response_code(409);
        echo json_encode(['error' => 'A user already exists with that email or force number']);
    } else {
        http_response_code(500);
        echo json_encode(['error' => 'Database error: ' . $e->getMessage()]);
    }
}
